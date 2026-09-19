using guideXOS.Kernel.Drivers;
using guideXOS.FS;
using guideXOS.Misc;
using guideXOS.OS;
using System;
using System.Windows.Forms;
using System.Drawing;
using guideXOS.GUI;
using System.Collections.Generic;

namespace guideXOS.GUI {
    internal class DisplayOptions : Window {
        private readonly ApplicationServiceContext _serviceContext;
        private readonly ApplicationServiceAccess _services;
        private int _padding = 16;
        private int _selectedResIndex = -1;
        private bool _confirmVisible = false;
        private int _countdown = 15;
        private Resolution _previous;
        private Resolution _pending;
        private ulong _lastCountdownTick;
        private string[] _resLabels;

        // Tabs
        private int _tabH = 40;
        private int _tabGap = 10;
        private int _currentTab = 0; // 0 = Desktop Background, 1 = Screen Resolution, 2 = Gradients, 3 = Font Settings

        // Background thumbnails
        private List<string> _backgroundPaths;
        private List<Image> _thumbnails;
        private int _thumbSize = 128; // Increased from 96 to 128
        private int _selectedBgIndex = -1;
        private bool _thumbnailsLoaded = false;
        private int _bgScroll = 0;
        private bool _bgScrollDrag = false;
        private int _bgScrollStartY, _bgScrollStartScroll;

        // Background UI buttons
        private int _btnW = 180;
        private int _btnH = 38;
        private ApplicationServiceRequestHandle _openRequest;
        private string _lastBackgroundPath;
        private ColorPicker _colorDlg;
        
        // Gradients scroll
        private int _gradientScroll = 0;
        private bool _gradientScrollDrag = false;
        private int _gradientScrollStartY, _gradientScrollStartScroll;

        // Font Settings
        private List<string> _fontPaths;
        private int _selectedFontIndex = -1;
        private int _fontScroll = 0;
        private bool _fontScrollDrag = false;
        private int _fontScrollStartY, _fontScrollStartScroll;
        private int _selectedFontSize = 18; // Default
        private int[] _fontSizes = new[] { 12, 14, 16, 18, 20, 24, 28, 32 };
        private bool _fontsLoaded = false;

        public DisplayOptions(int X, int Y, int W = 800, int H = 600) : base(X, Y, W, H) {
            _serviceContext = null;
            _services = null;
            InitializeDisplayOptions();
        }

        public DisplayOptions(int X, int Y, int W, int H,
                ApplicationServiceContext serviceContext,
                ApplicationServiceAccess services) : base(X, Y, W, H) {
            _serviceContext = serviceContext;
            _services = services;
            InitializeDisplayOptions();
        }

        private void InitializeDisplayOptions() {
            IsResizable = false;
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowTombstone = false;
            Title = "Display Options";
            _openRequest = ApplicationServiceRequestHandle.Invalid;
            _lastBackgroundPath = null;
            
            // Initialize resolution list
            var list = DisplayManager.AvailableResolutions;
            if (list != null && list.Length > 0) {
                var cur = DisplayManager.Current;
                _selectedResIndex = 0;
                for (int i = 0; i < list.Length; i++) {
                    if (list[i].Width == cur.Width && list[i].Height == cur.Height) { _selectedResIndex = i; break; }
                }
                _resLabels = new string[list.Length]; 
                for (int i = 0; i < list.Length; i++) 
                    _resLabels[i] = list[i].Width.ToString() + " x " + list[i].Height.ToString();
            } else {
                _resLabels = new string[0]; 
                _selectedResIndex = -1;
            }

            // Initialize background lists
            _backgroundPaths = new List<string>();
            _thumbnails = new List<Image>();
            
            // Load backgrounds immediately
            LoadBackgrounds();

            // Initialize font lists
            _fontPaths = new List<string>();
            _selectedFontSize = WindowManager.font != null ? WindowManager.font.FontSize : 18;
        }

        private void LoadBackgrounds() {
            if (_thumbnailsLoaded) return;
            _thumbnailsLoaded = true;

            var files = File.GetFiles(@"Backgrounds/");
            if (files != null && files.Count > 0) {
                for (int i = 0; i < files.Count; i++) {
                    var fi = files[i];
                    if (fi.Attribute != FileAttribute.Directory) {
                        string name = fi.Name;
                        // Check for image files - case insensitive
                        bool isPng = name.EndsWith(".png") || name.EndsWith(".PNG");
                        bool isJpg = name.EndsWith(".jpg") || name.EndsWith(".JPG") || name.EndsWith(".jpeg") || name.EndsWith(".JPEG");
                        bool isBmp = name.EndsWith(".bmp") || name.EndsWith(".BMP");
                        
                        // Skip thumbnail files themselves
                        if (name.EndsWith("_thumb.png") || name.EndsWith("_thumb.PNG")) {
                            continue;
                        }
                        
                        if (isPng || isJpg || isBmp) {
                            string path = "Backgrounds/" + name;
                            _backgroundPaths.Add(path);
                            
                            // Try to find existing thumbnail first
                            string thumbPath = null;
                            if (isPng) {
                                // Remove .png extension and add _thumb.png
                                string baseName = name.Substring(0, name.Length - 4);
                                thumbPath = "Backgrounds/" + baseName + "_thumb.png";
                            } else if (isJpg) {
                                // Remove .jpg/.jpeg extension and add _thumb.png
                                int extLen = name.EndsWith(".jpeg") || name.EndsWith(".JPEG") ? 5 : 4;
                                string baseName = name.Substring(0, name.Length - extLen);
                                thumbPath = "Backgrounds/" + baseName + "_thumb.png";
                            } else if (isBmp) {
                                // Remove .bmp extension and add _thumb.png
                                string baseName = name.Substring(0, name.Length - 4);
                                thumbPath = "Backgrounds/" + baseName + "_thumb.png";
                            }
                            
                            Image thumb = null;
                            bool thumbLoaded = false;
                            
                            // Try to load existing thumbnail
                            if (thumbPath != null) {
                                try {
                                    byte[] thumbData = File.ReadAllBytes(thumbPath);
                                    if (thumbData != null && thumbData.Length > 0) {
                                        var thumbImg = new PNG(thumbData);
                                        thumb = thumbImg;
                                        thumbLoaded = true;
                                    }
                                } catch {
                                    // Thumbnail doesn't exist or failed to load, will generate instead
                                }
                            }
                            
                            // If no thumbnail exists, generate one from the full image
                            if (!thumbLoaded) {
                                try {
                                    byte[] data = File.ReadAllBytes(path);
                                    if (data != null && data.Length > 0) {
                                        // Create PNG from data
                                        var fullImg = new PNG(data);
                                        
                                        // Calculate aspect-fit resize for thumbnail
                                        int thumbW = _thumbSize;
                                        int thumbH = _thumbSize;
                                        
                                        if (fullImg.Width > fullImg.Height) {
                                            thumbH = (fullImg.Height * _thumbSize) / fullImg.Width;
                                        } else if (fullImg.Height > fullImg.Width) {
                                            thumbW = (fullImg.Width * _thumbSize) / fullImg.Height;
                                        }
                                        
                                        // Ensure we don't have zero dimensions
                                        if (thumbW < 1) thumbW = 1;
                                        if (thumbH < 1) thumbH = 1;
                                        
                                        // Resize to thumbnail
                                        thumb = fullImg.ResizeImage(thumbW, thumbH);
                                        fullImg.Dispose();
                                    }
                                } catch {
                                    // Failed to load or generate thumbnail
                                }
                            }
                            
                            // Add thumbnail (or null if failed)
                            _thumbnails.Add(thumb);
                        }
                    }
                    fi.Dispose();
                }
                files.Dispose();
            }
        }

        private void LoadFonts() {
            if (_fontsLoaded) return;
            _fontsLoaded = true;

            _fontPaths.Clear();
            var files = File.GetFiles(@"Fonts/");
            if (files != null && files.Count > 0) {
                for (int i = 0; i < files.Count; i++) {
                    var fi = files[i];
                    if (fi.Attribute != FileAttribute.Directory) {
                        string name = fi.Name;
                        if (name.EndsWith(".png") || name.EndsWith(".PNG")) {
                            _fontPaths.Add("Fonts/" + name);
                        }
                    }
                    fi.Dispose();
                }
                files.Dispose();
            }
        }

        public override void OnInput() {
            base.OnInput(); 
            if (!Visible) return;

            PollBackgroundOpen();
            
            // Modal dialogs take precedence - don't process any input if dialogs are visible
            if (_openRequest.IsValid) return;
            if (_colorDlg != null && _colorDlg.Visible) return;
            
            // Only process input if mouse is within the window bounds
            if (!IsUnderMouse()) return;

            int cx = X + _padding; 
            int cy = Y + _padding; 
            int cw = Width - _padding * 2; 
            int contentY = cy + _tabH + _tabGap;
            int contentH = Height - _padding * 2 - _tabH - _tabGap;
            int mx = Control.MousePosition.X; 
            int my = Control.MousePosition.Y;

            if (Control.MouseButtons == MouseButtons.Left) {
                // Tab clicks
                int tabW = (cw - _tabGap * 3) / 4; // 4 tabs now
                int tabX0 = cx; 
                int tabX1 = cx + tabW + _tabGap; 
                int tabX2 = cx + (tabW + _tabGap) * 2;
                int tabX3 = cx + (tabW + _tabGap) * 3;
                int tabY = cy;
                if (mx >= tabX0 && mx <= tabX0 + tabW && my >= tabY && my <= tabY + _tabH) { 
                    _currentTab = 0; 
                    return; 
                }
                if (mx >= tabX1 && mx <= tabX1 + tabW && my >= tabY && my <= tabY + _tabH) { 
                    _currentTab = 1; 
                    return; 
                }
                if (mx >= tabX2 && mx <= tabX2 + tabW && my >= tabY && my <= tabY + _tabH) { 
                    _currentTab = 2; 
                    return; 
                }
                if (mx >= tabX3 && mx <= tabX3 + tabW && my >= tabY && my <= tabY + _tabH) { 
                    _currentTab = 3;
                    if (!_fontsLoaded) LoadFonts();
                    return; 
                }

                // Desktop Background tab
                if (_currentTab == 0) {
                    // Gallery area
                    int galleryY = contentY + WindowManager.font.FontSize + 16;
                    int galleryH = contentH - WindowManager.font.FontSize - 16 - _btnH - 28;
                    int galleryW = cw - 14; // Account for scrollbar
                    
                    // Calculate grid
                    int thumbPad = 10;
                    int cellSize = _thumbSize + thumbPad * 2;
                    int cols = galleryW / cellSize;
                    if (cols < 1) cols = 1;

                    // Thumbnail clicks
                    for (int i = 0; i < _backgroundPaths.Count; i++) {
                        int col = i % cols;
                        int row = i / cols;
                        int tx = cx + col * cellSize + thumbPad;
                        int ty = galleryY + row * cellSize + thumbPad - _bgScroll;
                        
                        if (ty + _thumbSize < galleryY || ty > galleryY + galleryH) continue;
                        
                        if (mx >= tx && mx <= tx + _thumbSize && my >= ty && my <= ty + _thumbSize) {
                            // Set background and close window
                            try {
                                byte[] data = File.ReadAllBytes(_backgroundPaths[i]);
                                if (data != null) {
                                    var img = new PNG(data);
                                    if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
                                    Program.Wallpaper = img.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                                    img.Dispose();
                                    this.Visible = false;
                                }
                            } catch {
                                NotifyApplication("Failed to load background", true);
                            }
                            return;
                        }
                    }

                    // Scrollbar for gallery
                    int sbW = 12;
                    int sbX = cx + cw - sbW;
                    if (mx >= sbX && mx <= sbX + sbW && my >= galleryY && my <= galleryY + galleryH) {
                        _bgScrollDrag = true;
                        _bgScrollStartY = my;
                        _bgScrollStartScroll = _bgScroll;
                        return;
                    }

                    // Buttons at bottom
                    int btnY = Y + Height - _padding - _btnH - 10;
                    int btnSelectX = cx;
                    int btnColorX = btnSelectX + _btnW + 16;
                    int btnEffectsX = btnColorX + _btnW + 16;

                    if (mx >= btnSelectX && mx <= btnSelectX + _btnW && my >= btnY && my <= btnY + _btnH) {
                        BeginBackgroundOpen();
                        return;
                    }
                    
                    if (mx >= btnColorX && mx <= btnColorX + _btnW && my >= btnY && my <= btnY + _btnH) {
                        // Open color picker
                        _colorDlg = new ColorPicker(X + 80, Y + 100, (color) => {
                            var img = new Image(Framebuffer.Width, Framebuffer.Height);
                            for (int yy = 0; yy < img.Height; yy++) 
                                for (int xx = 0; xx < img.Width; xx++) 
                                    img.RawData[yy * img.Width + xx] = (int)color;
                            if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
                            Program.Wallpaper = img;
                        });
                        WindowManager.MoveToEnd(_colorDlg); 
                        _colorDlg.Visible = true; 
                        return;
                    }
                    
                    if (mx >= btnEffectsX && mx <= btnEffectsX + _btnW && my >= btnY && my <= btnY + _btnH) {
                        // Open Visual Effects settings
                        var effectsWindow = new VisualEffectsSettings(X + 60, Y + 40);
                        WindowManager.MoveToEnd(effectsWindow);
                        effectsWindow.Visible = true;
                        return;
                    }
                }
                
                // Gradients tab
                if (_currentTab == 2) {
                    int galleryY = contentY + WindowManager.font.FontSize + 16;
                    int galleryH = contentH - WindowManager.font.FontSize - 16;
                    int galleryW = cw - 14;
                    
                    int thumbPad = 10;
                    int cellSize = _thumbSize + thumbPad * 2;
                    int cols = galleryW / cellSize;
                    if (cols < 1) cols = 1;
                    
                    int gradientCount = 12;
                    
                    // Gradient thumbnail clicks
                    for (int i = 0; i < gradientCount; i++) {
                        int col = i % cols;
                        int row = i / cols;
                        int tx = cx + col * cellSize + thumbPad;
                        int ty = galleryY + row * cellSize + thumbPad - _gradientScroll;
                        
                        if (ty + _thumbSize < galleryY || ty > galleryY + galleryH) continue;
                        
                        if (mx >= tx && mx <= tx + _thumbSize && my >= ty && my <= ty + _thumbSize) {
                            ApplyGradient(i);
                            this.Visible = false;
                            return;
                        }
                    }
                    
                    // Scrollbar for gradients
                    int sbW = 12;
                    int sbX = cx + cw - sbW;
                    if (mx >= sbX && mx <= sbX + sbW && my >= galleryY && my <= galleryY + galleryH) {
                        _gradientScrollDrag = true;
                        _gradientScrollStartY = my;
                        _gradientScrollStartScroll = _gradientScroll;
                        return;
                    }
                }
                
                // Screen Resolution tab
                if (_currentTab == 1) {
                    var list = DisplayManager.AvailableResolutions;
                    if (list != null && list.Length > 0) {
                        int listX = cx;
                        int listY = contentY + WindowManager.font.FontSize + 16;
                        int listW = cw;
                        int itemHeight = 42;
                        
                        for (int i = 0; i < list.Length; i++) {
                            int rowY = listY + i * itemHeight;
                            if (mx >= listX && mx <= listX + listW && my >= rowY && my <= rowY + itemHeight - 2) {
                                if (i != _selectedResIndex) {
                                    _selectedResIndex = i;
                                    _previous = DisplayManager.Current;
                                    if (DisplayManager.TrySetResolution(list[i].Width, list[i].Height)) {
                                        _pending = list[i];
                                        _confirmVisible = true;
                                        _countdown = 15;
                                        _lastCountdownTick = Timer.Ticks;
                                    } else {
                                        NotifyApplication("Failed to set resolution", true);
                                    }
                                }
                                return;
                            }
                        }
                    }
                    
                    // Confirmation buttons
                    if (_confirmVisible) {
                        int confirmBtnW = 110, confirmBtnH = 38, gap = 14;
                        int btnY = Y + Height - _padding - confirmBtnH - 10;
                        int yesX = X + Width - _padding - confirmBtnW;
                        int noX = yesX - gap - confirmBtnW;
                        
                        if (mx >= noX && mx <= noX + confirmBtnW && my >= btnY && my <= btnY + confirmBtnH) {
                            RevertResolution();
                            return;
                        }
                        if (mx >= yesX && mx <= yesX + confirmBtnW && my >= btnY && my <= btnY + confirmBtnH) {
                            KeepResolution();
                            return;
                        }
                    }
                }

                // Font Settings tab
                if (_currentTab == 3) {
                    int listY = contentY + WindowManager.font.FontSize + 16;
                    int listH = contentH - WindowManager.font.FontSize - 16 - _btnH - 20;
                    int listW = cw - 14;
                    int itemH = 40;

                    // Font list clicks
                    for (int i = 0; i < _fontPaths.Count; i++) {
                        int rowY = listY + i * itemH - _fontScroll;
                        if (rowY + itemH < listY || rowY > listY + listH) continue;
                        
                        if (mx >= cx && mx <= cx + listW && my >= rowY && my <= rowY + itemH - 2) {
                            _selectedFontIndex = i;
                            return;
                        }
                    }

                    // Scrollbar for font list
                    int sbW = 12;
                    int sbX = cx + cw - sbW;
                    if (mx >= sbX && mx <= sbX + sbW && my >= listY && my <= listY + listH) {
                        _fontScrollDrag = true;
                        _fontScrollStartY = my;
                        _fontScrollStartScroll = _fontScroll;
                        return;
                    }

                    // Font size buttons
                    int sizeBtnY = listY + listH + 10;
                    int sizeBtnW = 60;
                    int sizeBtnH = 32;
                    int sizeGap = 8;
                    int sizeX = cx;

                    for (int i = 0; i < _fontSizes.Length; i++) {
                        if (mx >= sizeX && mx <= sizeX + sizeBtnW && my >= sizeBtnY && my <= sizeBtnY + sizeBtnH) {
                            _selectedFontSize = _fontSizes[i];
                            return;
                        }
                        sizeX += sizeBtnW + sizeGap;
                    }

                    // Apply button
                    int applyBtnW = 120;
                    int applyBtnH = 38;
                    int applyBtnX = X + Width - _padding - applyBtnW;
                    int applyBtnY = Y + Height - _padding - applyBtnH - 10;

                    if (mx >= applyBtnX && mx <= applyBtnX + applyBtnW && my >= applyBtnY && my <= applyBtnY + applyBtnH) {
                        ApplyFont();
                        return;
                    }
                }
            } else {
                _bgScrollDrag = false;
                _gradientScrollDrag = false;
                _fontScrollDrag = false;
            }

            // Background gallery scrolling
            if (_bgScrollDrag && _currentTab == 0) {
                int galleryY = contentY + WindowManager.font.FontSize + 16;
                int galleryH = Height - _padding * 2 - _tabH - _tabGap - WindowManager.font.FontSize - 16 - _btnH - 28;
                int thumbPad = 10;
                int cellSize = _thumbSize + thumbPad * 2;
                int cols = (Width - _padding * 2 - 14) / cellSize;
                if (cols < 1) cols = 1;
                int rows = (_backgroundPaths.Count + cols - 1) / cols;
                int totalH = rows * cellSize;
                int maxScroll = totalH > galleryH ? totalH - galleryH : 0;
                
                int dy = my - _bgScrollStartY;
                _bgScroll = _bgScrollStartScroll + dy;
                if (_bgScroll < 0) _bgScroll = 0;
                if (_bgScroll > maxScroll) _bgScroll = maxScroll;
            }
            
            // Gradient gallery scrolling
            if (_gradientScrollDrag && _currentTab == 2) {
                int galleryY = contentY + WindowManager.font.FontSize + 16;
                int galleryH = Height - _padding * 2 - _tabH - _tabGap - WindowManager.font.FontSize - 16;
                int thumbPad = 10;
                int cellSize = _thumbSize + thumbPad * 2;
                int cols = (Width - _padding * 2 - 14) / cellSize;
                if (cols < 1) cols = 1;
                int gradientCount = 12; // We'll show 12 preset gradients
                int rows = (gradientCount + cols - 1) / cols;
                int totalH = rows * cellSize;
                int maxScroll = totalH > galleryH ? totalH - galleryH : 0;
                
                int dy = my - _gradientScrollStartY;
                _gradientScroll = _gradientScrollStartScroll + dy;
                if (_gradientScroll < 0) _gradientScroll = 0;
                if (_gradientScroll > maxScroll) _gradientScroll = maxScroll;
            }

            // Font list scrolling
            if (_fontScrollDrag && _currentTab == 3) {
                int listY = contentY + WindowManager.font.FontSize + 16;
                int listH = contentH - WindowManager.font.FontSize - 16 - _btnH - 20;
                int itemH = 40;
                int totalH = _fontPaths.Count * itemH;
                int maxScroll = totalH > listH ? totalH - listH : 0;
                
                int dy = my - _fontScrollStartY;
                _fontScroll = _fontScrollStartScroll + dy;
                if (_fontScroll < 0) _fontScroll = 0;
                if (_fontScroll > maxScroll) _fontScroll = maxScroll;
            }
            
            // Handle resolution confirmation countdown
            if (_confirmVisible && _currentTab == 1) {
                ulong elapsed = Timer.Ticks - _lastCountdownTick;
                if (elapsed >= 1000) {
                    _countdown--;
                    _lastCountdownTick = Timer.Ticks;
                    if (_countdown <= 0) {
                        RevertResolution();
                    }
                }
            }
        }
        
        private void KeepResolution() { 
            _confirmVisible = false; 
            DisplayManager.SaveResolution(_pending); 
        }
        
        private void RevertResolution() { 
            _confirmVisible = false; 
            if (!DisplayManager.TrySetResolution(_previous.Width, _previous.Height)) { 
                NotifyApplication("Failed to revert resolution", true);
            } else { 
                var list = DisplayManager.AvailableResolutions; 
                if (list != null) { 
                    for (int i = 0; i < list.Length; i++) 
                        if (list[i].Width == _previous.Width && list[i].Height == _previous.Height) { 
                            _selectedResIndex = i; 
                            break; 
                        } 
                } 
            } 
        }
        
        private void ApplyGradient(int index) {
            var img = new Image(Framebuffer.Width, Framebuffer.Height);
            
            // Define 12 different gradient presets
            switch (index) {
                case 0: // Blue to Purple
                    CreateGradient(img, 0xFF1E3A8A, 0xFF7C3AED, true);
                    break;
                case 1: // Green to Teal
                    CreateGradient(img, 0xFF065F46, 0xFF0891B2, true);
                    break;
                case 2: // Orange to Pink
                    CreateGradient(img, 0xFFEA580C, 0xFFEC4899, true);
                    break;
                case 3: // Purple to Pink
                    CreateGradient(img, 0xFF6B21A8, 0xFFDB2777, true);
                    break;
                case 4: // Dark Blue to Cyan
                    CreateGradient(img, 0xFF1E40AF, 0xFF06B6D4, true);
                    break;
                case 5: // Red to Orange
                    CreateGradient(img, 0xFFB91C1C, 0xFFEA580C, true);
                    break;
                case 6: // Teal to Green
                    CreateGradient(img, 0xFF115E59, 0xFF16A34A, false);
                    break;
                case 7: // Pink to Yellow
                    CreateGradient(img, 0xFFDB2777, 0xFFF59E0B, false);
                    break;
                case 8: // Deep Purple to Blue
                    CreateGradient(img, 0xFF4C1D95, 0xFF1D4ED8, true);
                    break;
                case 9: // Indigo to Purple
                    CreateGradient(img, 0xFF3730A3, 0xFF7E22CE, true);
                    break;
                case 10: // Dark Gray to Light Gray
                    CreateGradient(img, 0xFF1F2937, 0xFF6B7280, true);
                    break;
                case 11: // Navy to Sky Blue
                    CreateGradient(img, 0xFF1E3A8A, 0xFF0EA5E9, false);
                    break;
            }
            
            if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
            Program.Wallpaper = img;
        }
        
        private void CreateGradient(Image img, uint startColor, uint endColor, bool vertical) {
            int startA = (int)(startColor >> 24);
            int startR = (int)((startColor >> 16) & 0xFF);
            int startG = (int)((startColor >> 8) & 0xFF);
            int startB = (int)(startColor & 0xFF);
            
            int endA = (int)(endColor >> 24);
            int endR = (int)((endColor >> 16) & 0xFF);
            int endG = (int)((endColor >> 8) & 0xFF);
            int endB = (int)(endColor & 0xFF);
            
            if (vertical) {
                // Top to bottom gradient
                for (int y = 0; y < img.Height; y++) {
                    float t = (float)y / img.Height;
                    int a = (int)(startA + (endA - startA) * t);
                    int r = (int)(startR + (endR - startR) * t);
                    int g = (int)(startG + (endG - startG) * t);
                    int b = (int)(startB + (endB - startB) * t);
                    uint color = (uint)((a << 24) | (r << 16) | (g << 8) | b);
                    
                    for (int x = 0; x < img.Width; x++) {
                        img.RawData[y * img.Width + x] = (int)color;
                    }
                }
            } else {
                // Left to right gradient
                for (int x = 0; x < img.Width; x++) {
                    float t = (float)x / img.Width;
                    int a = (int)(startA + (endA - startA) * t);
                    int r = (int)(startR + (endR - startR) * t);
                    int g = (int)(startG + (endG - startG) * t);
                    int b = (int)(startB + (endB - startB) * t);
                    uint color = (uint)((a << 24) | (r << 16) | (g << 8) | b);
                    
                    for (int y = 0; y < img.Height; y++) {
                        img.RawData[y * img.Width + x] = (int)color;
                    }
                }
            }
        }

        private void DrawGradientPreview(int index, int x, int y, int w, int h) {
            // Define gradient colors matching ApplyGradient
            uint startColor = 0xFF000000;
            uint endColor = 0xFF000000;
            bool vertical = true;
            
            switch (index) {
                case 0: startColor = 0xFF1E3A8A; endColor = 0xFF7C3AED; vertical = true; break;
                case 1: startColor = 0xFF065F46; endColor = 0xFF0891B2; vertical = true; break;
                case 2: startColor = 0xFFEA580C; endColor = 0xFFEC4899; vertical = true; break;
                case 3: startColor = 0xFF6B21A8; endColor = 0xFFDB2777; vertical = true; break;
                case 4: startColor = 0xFF1E40AF; endColor = 0xFF06B6D4; vertical = true; break;
                case 5: startColor = 0xFFB91C1C; endColor = 0xFFEA580C; vertical = true; break;
                case 6: startColor = 0xFF115E59; endColor = 0xFF16A34A; vertical = false; break;
                case 7: startColor = 0xFFDB2777; endColor = 0xFFF59E0B; vertical = false; break;
                case 8: startColor = 0xFF4C1D95; endColor = 0xFF1D4ED8; vertical = true; break;
                case 9: startColor = 0xFF3730A3; endColor = 0xFF7E22CE; vertical = true; break;
                case 10: startColor = 0xFF1F2937; endColor = 0xFF6B7280; vertical = true; break;
                case 11: startColor = 0xFF1E3A8A; endColor = 0xFF0EA5E9; vertical = false; break;
            }
            
            int startA = (int)(startColor >> 24);
            int startR = (int)((startColor >> 16) & 0xFF);
            int startG = (int)((startColor >> 8) & 0xFF);
            int startB = (int)(startColor & 0xFF);
            
            int endA = (int)(endColor >> 24);
            int endR = (int)((endColor >> 16) & 0xFF);
            int endG = (int)((endColor >> 8) & 0xFF);
            int endB = (int)(endColor & 0xFF);
            
            if (vertical) {
                for (int py = 0; py < h; py++) {
                    float t = (float)py / h;
                    int a = (int)(startA + (endA - startA) * t);
                    int r = (int)(startR + (endR - startR) * t);
                    int g = (int)(startG + (endG - startG) * t);
                    int b = (int)(startB + (endB - startB) * t);
                    uint color = (uint)((a << 24) | (r << 16) | (g << 8) | b);
                    
                    Framebuffer.Graphics.FillRectangle(x, y + py, w, 1, color);
                }
            } else {
                for (int px = 0; px < w; px++) {
                    float t = (float)px / w;
                    int a = (int)(startA + (endA - startA) * t);
                    int r = (int)(startR + (endR - startR) * t);
                    int g = (int)(startG + (endG - startG) * t);
                    int b = (int)(startB + (endB - startB) * t);
                    uint color = (uint)((a << 24) | (r << 16) | (g << 8) | b);
                    
                    Framebuffer.Graphics.FillRectangle(x + px, y, 1, h, color);
                }
            }
        }

        private void ApplyFont() {
            if (_selectedFontIndex < 0 || _selectedFontIndex >= _fontPaths.Count) {
                NotifyApplication("Please select a font", true);
                return;
            }

            try {
                string fontPath = _fontPaths[_selectedFontIndex];
                byte[] fontData = File.ReadAllBytes(fontPath);
                if (fontData == null || fontData.Length == 0) {
                    NotifyApplication("Failed to load font file", true);
                    return;
                }

                var fontImg = new PNG(fontData);

                // Create new font with selected size
                // FIXED: Added leading space to charset to match WindowManager.cs and generate_font.py
                var newFont = new IFont(
                    fontImg,
                    " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~",
                    _selectedFontSize
                );

                // Dispose old font if needed
                if (WindowManager.font != null) {
                    // Note: We can't easily dispose the old font image without breaking references
                    // Just replace the reference
                }

                WindowManager.font = newFont;
                
                NotifyApplication("Font applied successfully", false);
                this.Visible = false;
            } catch {
                NotifyApplication("Failed to apply font", true);
            }
        }

        private void NotifyApplication(string message, bool error) {
            if (_serviceContext == null || _services == null ||
                    _services.Notifications == null) return;
            _services.Notifications.Publish(_serviceContext,
                ApplicationNotificationRequest.Create(
                    "Display Options", message,
                    error ? ApplicationNotificationSeverity.Error :
                        ApplicationNotificationSeverity.Info));
        }

        private void BeginBackgroundOpen() {
            if (_openRequest.IsValid) return;
            if (_serviceContext == null || _services == null ||
                    _services.OpenFile == null) {
                NotifyApplication("Open service unavailable", true);
                return;
            }
            OpenFileRequest request = OpenFileRequest.Create("Backgrounds");
            ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                _services.OpenFile.Begin(_serviceContext, request);
            if (begun.Succeeded) {
                _openRequest = begun.Value;
            } else {
                NotifyApplication("Failed to open background picker", true);
            }
        }

        private void PollBackgroundOpen() {
            if (!_openRequest.IsValid || _serviceContext == null ||
                    _services == null || _services.OpenFile == null) return;
            ApplicationServiceResult<ApplicationServiceRequestStatus<
                ApplicationFileDialogResult>> observed =
                _services.OpenFile.Observe(_serviceContext, _openRequest);
            if (!observed.Succeeded || observed.Value == null ||
                    !observed.Value.IsTerminal) return;
            ApplicationFileDialogResult result = observed.Value.Value;
            ApplicationServiceRequestState state = observed.Value.State;
            _openRequest = ApplicationServiceRequestHandle.Invalid;
            if (state == ApplicationServiceRequestState.Completed &&
                    result != null && result.Outcome ==
                    ApplicationFileDialogOutcome.Selected) {
                ApplySelectedBackground(result.SelectedPath);
            }
        }

        private void ApplySelectedBackground(string path) {
            if (string.IsNullOrEmpty(path)) {
                NotifyApplication("Selected background is invalid", true);
                return;
            }
            try {
                byte[] imgData = File.ReadAllBytes(path);
                var img = new PNG(imgData);
                if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
                Program.Wallpaper = img.ResizeImage(Framebuffer.Width,
                    Framebuffer.Height);
                img.Dispose();
                _lastBackgroundPath = path;
            } catch {
                NotifyApplication("Failed to load image", true);
            }
        }

        internal bool RunPhase9BackgroundServiceDiagnostic() {
            bool success = false;
            bool cancelled = false;
            try {
                if (_serviceContext == null || _services == null ||
                        _services.OpenFile == null) return false;
                OpenFileRequest request = OpenFileRequest.Create("Backgrounds");
                ApplicationServiceResult<ApplicationServiceRequestHandle> begun =
                    _services.OpenFile.Begin(_serviceContext, request);
                if (!begun.Succeeded) return false;
                _openRequest = begun.Value;
                ApplicationServiceResult completed =
                    ApplicationServiceRegistry.CompleteFileDialogRequestForSelfTest(
                        _openRequest, ApplicationFileDialogOutcome.Selected,
                        "Backgrounds/dinos.png");
                PollBackgroundOpen();
                success = completed.Succeeded &&
                    _lastBackgroundPath == "Backgrounds/dinos.png" &&
                    !_openRequest.IsValid;

                begun = _services.OpenFile.Begin(_serviceContext, request);
                if (!begun.Succeeded) return false;
                _openRequest = begun.Value;
                ApplicationServiceResult cancelledResult =
                    _services.OpenFile.Cancel(_serviceContext, _openRequest);
                PollBackgroundOpen();
                cancelled = cancelledResult.Code ==
                    ApplicationServiceResultCode.Cancelled &&
                    !_openRequest.IsValid;
            } catch {
                return false;
            }
#if UEFI_DIAGNOSTIC_APP_RUNTIME
            Program.MarkUefiAppRuntime("DISPLAY_OPEN_SUCCESS=" +
                (success ? "PASS" : "FAIL"));
            Program.MarkUefiAppRuntime("DISPLAY_OPEN_CANCEL=" +
                (cancelled ? "PASS" : "FAIL"));
            Program.MarkUefiAppRuntime("DISPLAY_DIALOG_CLEANUP=" +
                ((!_openRequest.IsValid &&
                    ApplicationServiceSessionTable.TransientWindowCount == 0) ?
                    "PASS" : "FAIL"));
#endif
            return success && cancelled && !_openRequest.IsValid;
        }

        public override void OnDraw() {
            PollBackgroundOpen();
            base.OnDraw(); 
            if (WindowManager.font == null) return;
            
            int cx = X + _padding; 
            int cy = Y + _padding; 
            int cw = Width - _padding * 2; 
            int contentY = cy + _tabH + _tabGap;
            int contentH = Height - _padding * 2 - _tabH - _tabGap;

            // Draw tabs
            int tabW = (cw - _tabGap * 3) / 4; // 4 tabs
            int tabX0 = cx; 
            int tabX1 = cx + tabW + _tabGap; 
            int tabX2 = cx + (tabW + _tabGap) * 2;
            int tabX3 = cx + (tabW + _tabGap) * 3;
            int tabY = cy;
            uint tabBg = 0xFF252525; 
            uint tabBgSel = 0xFF3A3A3A; 
            uint border = 0xFF4F4F4F;
            
            Framebuffer.Graphics.FillRectangle(tabX0, tabY, tabW, _tabH, _currentTab == 0 ? tabBgSel : tabBg);
            Framebuffer.Graphics.FillRectangle(tabX1, tabY, tabW, _tabH, _currentTab == 1 ? tabBgSel : tabBg);
            Framebuffer.Graphics.FillRectangle(tabX2, tabY, tabW, _tabH, _currentTab == 2 ? tabBgSel : tabBg);
            Framebuffer.Graphics.FillRectangle(tabX3, tabY, tabW, _tabH, _currentTab == 3 ? tabBgSel : tabBg);
            Framebuffer.Graphics.DrawRectangle(tabX0, tabY, tabW, _tabH, border, 1); 
            Framebuffer.Graphics.DrawRectangle(tabX1, tabY, tabW, _tabH, border, 1);
            Framebuffer.Graphics.DrawRectangle(tabX2, tabY, tabW, _tabH, border, 1);
            Framebuffer.Graphics.DrawRectangle(tabX3, tabY, tabW, _tabH, border, 1);
            
            int textY = tabY + (_tabH / 2 - WindowManager.font.FontSize / 2);
            WindowManager.font.DrawString(tabX0 + 10, textY, "Backgrounds");
            WindowManager.font.DrawString(tabX1 + 10, textY, "Resolution");
            WindowManager.font.DrawString(tabX2 + 10, textY, "Gradients");
            WindowManager.font.DrawString(tabX3 + 10, textY, "Fonts");

            // Draw content based on tab
            if (_currentTab == 0) {
                // Desktop Background tab
                WindowManager.font.DrawString(cx, contentY, "Select a background from the gallery:");
                
                // Gallery area
                int galleryY = contentY + WindowManager.font.FontSize + 16;
                int galleryH = contentH - WindowManager.font.FontSize - 16 - _btnH - 28;
                int galleryW = cw - 14; // Account for scrollbar
                
                Framebuffer.Graphics.FillRectangle(cx, galleryY, galleryW + 14, galleryH, 0xFF1A1A1A);
                
                // Show count or "no images" message
                if (_backgroundPaths.Count == 0) {
                    WindowManager.font.DrawString(cx + 20, galleryY + 20, "No images found in /Backgrounds directory");
                } else {
                    // Draw thumbnails in grid
                    int thumbPad = 10;
                    int cellSize = _thumbSize + thumbPad * 2;
                    int cols = galleryW / cellSize;
                    if (cols < 1) cols = 1;
                    
                    int mx = Control.MousePosition.X;
                    int my = Control.MousePosition.Y;
                    
                    for (int i = 0; i < _thumbnails.Count; i++) {
                        int col = i % cols;
                        int row = i / cols;
                        int tx = cx + col * cellSize + thumbPad;
                        int ty = galleryY + row * cellSize + thumbPad - _bgScroll;
                        
                        if (ty + _thumbSize < galleryY || ty > galleryY + galleryH) continue;
                        
                        // Draw cell background
                        Framebuffer.Graphics.FillRectangle(tx - 2, ty - 2, _thumbSize + 4, _thumbSize + 4, 0xFF2A2A2A);
                        
                        // Highlight on hover
                        bool hover = mx >= tx - 2 && mx <= tx + _thumbSize + 2 && my >= ty - 2 && my <= ty + _thumbSize + 2;
                        if (hover) {
                            Framebuffer.Graphics.FillRectangle(tx - 3, ty - 3, _thumbSize + 6, _thumbSize + 6, 0xFF4A7FBF);
                        }
                        
                        if (_thumbnails[i] != null) {
                            // Center the thumbnail in the cell
                            int imgW = _thumbnails[i].Width;
                            int imgH = _thumbnails[i].Height;
                            int offsetX = (_thumbSize - imgW) / 2;
                            int offsetY = (_thumbSize - imgH) / 2;
                            Framebuffer.Graphics.DrawImage(tx + offsetX, ty + offsetY, _thumbnails[i]);
                        } else {
                            // Failed to load indicator
                            Framebuffer.Graphics.FillRectangle(tx, ty, _thumbSize, _thumbSize, 0xFF444444);
                            WindowManager.font.DrawString(tx + _thumbSize/2 - 8, ty + _thumbSize / 2 - WindowManager.font.FontSize / 2, "?");
                        }
                        
                        // Draw border around thumbnail
                        Framebuffer.Graphics.DrawRectangle(tx, ty, _thumbSize, _thumbSize, 0xFF555555, 1);
                    }
                }
                
                // Scrollbar
                int sbW = 12;
                int sbX = cx + cw - sbW;
                int rows = (_thumbnails.Count + (galleryW / (_thumbSize + 20)) - 1) / (galleryW / (_thumbSize + 20));
                if (rows < 1) rows = 1;
                int totalH = rows * (_thumbSize + 20);
                int maxScroll = totalH > galleryH ? totalH - galleryH : 0;
                
                Framebuffer.Graphics.FillRectangle(sbX, galleryY, sbW, galleryH, 0xFF0F0F0F);
                if (maxScroll > 0 && totalH > 0) {
                    int thumbH = galleryH * galleryH / totalH;
                    if (thumbH < 24) thumbH = 24;
                    if (thumbH > galleryH) thumbH = galleryH;
                    int thumbY = galleryH * _bgScroll / totalH;
                    if (thumbY + thumbH > galleryH) thumbY = galleryH - thumbH;
                    Framebuffer.Graphics.FillRectangle(sbX + 1, galleryY + thumbY, sbW - 2, thumbH, 0xFF3F3F3F);
                }
                
                // Buttons at bottom
                int btnY = Y + Height - _padding - _btnH - 10;
                int btnSelectX = cx;
                int btnColorX = btnSelectX + _btnW + 16;
                int btnEffectsX = btnColorX + _btnW + 16;
                
                Framebuffer.Graphics.FillRectangle(btnSelectX, btnY, _btnW, _btnH, 0xFF2A2A2A);
                Framebuffer.Graphics.DrawRectangle(btnSelectX, btnY, _btnW, _btnH, 0xFF4F4F4F, 1);
                WindowManager.font.DrawString(btnSelectX + 16, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), "Select Background");
                
                Framebuffer.Graphics.FillRectangle(btnColorX, btnY, _btnW, _btnH, 0xFF2A2A2A);
                Framebuffer.Graphics.DrawRectangle(btnColorX, btnY, _btnW, _btnH, 0xFF4F4F4F, 1);
                WindowManager.font.DrawString(btnColorX + 24, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), "Choose Color");
                
                Framebuffer.Graphics.FillRectangle(btnEffectsX, btnY, _btnW, _btnH, 0xFF2A2A2A);
                Framebuffer.Graphics.DrawRectangle(btnEffectsX, btnY, _btnW, _btnH, 0xFF4F4F4F, 1);
                WindowManager.font.DrawString(btnEffectsX + 18, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), "Visual Effects");

            } else if (_currentTab == 1) {
                // Screen Resolution tab
                WindowManager.font.DrawString(cx, contentY, "Available resolutions:");
                
                var list = DisplayManager.AvailableResolutions; 
                if (list == null || list.Length == 0) {
                    WindowManager.font.DrawString(cx, contentY + WindowManager.font.FontSize + 28, "No resolutions available");
                    return;
                }
                
                int listX = cx; 
                int listY = contentY + WindowManager.font.FontSize + 16; 
                int listW = cw;
                int itemHeight = 42;
                
                for (int i = 0; i < list.Length; i++) { 
                    int rowY = listY + i * itemHeight; 
                    bool selected = i == _selectedResIndex; 
                    uint rowBg = selected ? 0xFF2E4A7F : 0xFF252525;
                    
                    Framebuffer.Graphics.FillRectangle(listX, rowY, listW, itemHeight - 2, rowBg);
                    Framebuffer.Graphics.DrawRectangle(listX, rowY, listW, itemHeight - 2, 0xFF3F3F3F, 1);
                    
                    string label = _resLabels != null && i < _resLabels.Length && _resLabels[i] != null ? 
                        _resLabels[i] : list[i].Width.ToString() + " x " + list[i].Height.ToString(); 
                    
                    WindowManager.font.DrawString(listX + 16, rowY + itemHeight / 2 - WindowManager.font.FontSize / 2, label);
                }
                
                // Confirmation dialog
                if (_confirmVisible) { 
                    int confirmBtnW = 110, confirmBtnH = 38, gap = 14; 
                    int btnY = Y + Height - _padding - confirmBtnH - 10; 
                    int yesX = X + Width - _padding - confirmBtnW; 
                    int noX = yesX - gap - confirmBtnW; 
                    
                    string msg = "Keep this resolution? Reverting in " + _countdown.ToString() + " seconds..."; 
                    WindowManager.font.DrawString(cx, btnY - WindowManager.font.FontSize - 10, msg); 
                    msg.Dispose();
                    
                    Framebuffer.Graphics.FillRectangle(noX, btnY, confirmBtnW, confirmBtnH, 0xFF2A2A2A); 
                    Framebuffer.Graphics.DrawRectangle(noX, btnY, confirmBtnW, confirmBtnH, 0xFF3F3F3F, 1); 
                    WindowManager.font.DrawString(noX + 36, btnY + confirmBtnH / 2 - WindowManager.font.FontSize / 2, "Revert"); 
                    
                    Framebuffer.Graphics.FillRectangle(yesX, btnY, confirmBtnW, confirmBtnH, 0xFF2E7F3F); 
                    Framebuffer.Graphics.DrawRectangle(yesX, btnY, confirmBtnW, confirmBtnH, 0xFF3F3F3F, 1); 
                    WindowManager.font.DrawString(yesX + 38, btnY + confirmBtnH / 2 - WindowManager.font.FontSize / 2, "Keep"); 
                }
            } else if (_currentTab == 2) {
                // Gradients tab
                WindowManager.font.DrawString(cx, contentY, "Select a gradient:");
                
                // Gallery area
                int galleryY = contentY + WindowManager.font.FontSize + 16;
                int galleryH = contentH - WindowManager.font.FontSize - 16;
                int galleryW = cw - 14; // Account for scrollbar
                
                Framebuffer.Graphics.FillRectangle(cx, galleryY, galleryW + 14, galleryH, 0xFF1A1A1A);
                
                // Draw gradients in grid
                int thumbPad = 10;
                int cellSize = _thumbSize + thumbPad * 2;
                int cols = galleryW / cellSize;
                if (cols < 1) cols = 1;
                
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                
                int gradientCount = 12;
                for (int i = 0; i < gradientCount; i++) {
                    int col = i % cols;
                    int row = i / cols;
                    int tx = cx + col * cellSize + thumbPad;
                    int ty = galleryY + row * cellSize + thumbPad - _gradientScroll;
                    
                    if (ty + _thumbSize < galleryY || ty > galleryY + galleryH) continue;
                    
                    // Draw cell background
                    Framebuffer.Graphics.FillRectangle(tx - 2, ty - 2, _thumbSize + 4, _thumbSize + 4, 0xFF2A2A2A);
                    
                    // Highlight on hover
                    bool hover = mx >= tx - 2 && mx <= tx + _thumbSize + 2 && my >= ty - 2 && my <= ty + _thumbSize + 2;
                    if (hover) {
                        Framebuffer.Graphics.FillRectangle(tx - 3, ty - 3, _thumbSize + 6, _thumbSize + 6, 0xFF4A7FBF);
                    }
                    
                    // Draw gradient preview
                    DrawGradientPreview(i, tx, ty, _thumbSize, _thumbSize);
                    
                    // Draw border around thumbnail
                    Framebuffer.Graphics.DrawRectangle(tx, ty, _thumbSize, _thumbSize, 0xFF555555, 1);
                }
                
                // Scrollbar
                int sbW = 12;
                int sbX = cx + cw - sbW;
                int rows = (gradientCount + cols - 1) / cols;
                if (rows < 1) rows = 1;
                int totalH = rows * cellSize;
                int maxScroll = totalH > galleryH ? totalH - galleryH : 0;
                
                Framebuffer.Graphics.FillRectangle(sbX, galleryY, sbW, galleryH, 0xFF0F0F0F);
                if (maxScroll > 0 && totalH > 0) {
                    int thumbH = galleryH * galleryH / totalH;
                    if (thumbH < 24) thumbH = 24;
                    if (thumbH > galleryH) thumbH = galleryH;
                    int thumbY = galleryH * _gradientScroll / totalH;
                    if (thumbY + thumbH > galleryH) thumbY = galleryH - thumbH;
                    Framebuffer.Graphics.FillRectangle(sbX + 1, galleryY + thumbY, sbW - 2, thumbH, 0xFF3F3F3F);
                }
            } else if (_currentTab == 3) {
                // Font Settings tab
                WindowManager.font.DrawString(cx, contentY, "Select a font and size:");

                if (!_fontsLoaded) {
                    WindowManager.font.DrawString(cx + 20, contentY + WindowManager.font.FontSize + 20, "Loading fonts...");
                    return;
                }

                int listY = contentY + WindowManager.font.FontSize + 16;
                int listH = contentH - WindowManager.font.FontSize - 16 - _btnH - 20;
                int listW = cw - 14;
                int itemH = 40;

                Framebuffer.Graphics.FillRectangle(cx, listY, listW + 14, listH, 0xFF1A1A1A);

                if (_fontPaths.Count == 0) {
                    WindowManager.font.DrawString(cx + 20, listY + 20, "No fonts found in /Fonts directory");
                } else {
                    // Draw font list
                    int mx = Control.MousePosition.X;
                    int my = Control.MousePosition.Y;

                    for (int i = 0; i < _fontPaths.Count; i++) {
                        int rowY = listY + i * itemH - _fontScroll;
                        if (rowY + itemH < listY || rowY > listY + listH) continue;

                        bool selected = i == _selectedFontIndex;
                        uint rowBg = selected ? 0xFF2E4A7F : 0xFF252525;

                        Framebuffer.Graphics.FillRectangle(cx, rowY, listW, itemH - 2, rowBg);
                        Framebuffer.Graphics.DrawRectangle(cx, rowY, listW, itemH - 2, 0xFF3F3F3F, 1);

                        // Extract font name from path
                        string fontPath = _fontPaths[i];
                        string fontName = fontPath;
                        int lastSlash = fontPath.LastIndexOf('/');
                        if (lastSlash >= 0 && lastSlash < fontPath.Length - 1) {
                            fontName = fontPath.Substring(lastSlash + 1);
                        }

                        WindowManager.font.DrawString(cx + 16, rowY + itemH / 2 - WindowManager.font.FontSize / 2, fontName, listW - 32, WindowManager.font.FontSize);
                    }

                    // Scrollbar
                    int sbW = 12;
                    int sbX = cx + cw - sbW;
                    int totalH = _fontPaths.Count * itemH;
                    int maxScroll = totalH > listH ? totalH - listH : 0;

                    Framebuffer.Graphics.FillRectangle(sbX, listY, sbW, listH, 0xFF0F0F0F);
                    if (maxScroll > 0 && totalH > 0) {
                        int thumbH = listH * listH / totalH;
                        if (thumbH < 24) thumbH = 24;
                        if (thumbH > listH) thumbH = listH;
                        int thumbY = listH * _fontScroll / totalH;
                        if (thumbY + thumbH > listH) thumbY = listH - thumbH;
                        Framebuffer.Graphics.FillRectangle(sbX + 1, listY + thumbY, sbW - 2, thumbH, 0xFF3F3F3F);
                    }
                }

                // Font size buttons
                int sizeBtnY = listY + listH + 10;
                int sizeBtnW = 60;
                int sizeBtnH = 32;
                int sizeGap = 8;
                int sizeX = cx;

                WindowManager.font.DrawString(cx, sizeBtnY - WindowManager.font.FontSize - 6, "Font Size:");

                for (int i = 0; i < _fontSizes.Length; i++) {
                    bool selected = _fontSizes[i] == _selectedFontSize;
                    uint btnBg = selected ? 0xFF2E4A7F : 0xFF2A2A2A;

                    Framebuffer.Graphics.FillRectangle(sizeX, sizeBtnY, sizeBtnW, sizeBtnH, btnBg);
                    Framebuffer.Graphics.DrawRectangle(sizeX, sizeBtnY, sizeBtnW, sizeBtnH, 0xFF4F4F4F, 1);

                    string sizeText = _fontSizes[i].ToString();
                    int textW = WindowManager.font.MeasureString(sizeText);
                    WindowManager.font.DrawString(sizeX + (sizeBtnW - textW) / 2, sizeBtnY + (sizeBtnH - WindowManager.font.FontSize) / 2, sizeText);

                    sizeX += sizeBtnW + sizeGap;
                }

                // Apply button
                int applyBtnW = 120;
                int applyBtnH = 38;
                int applyBtnX = X + Width - _padding - applyBtnW;
                int applyBtnY = Y + Height - _padding - applyBtnH - 10;

                Framebuffer.Graphics.FillRectangle(applyBtnX, applyBtnY, applyBtnW, applyBtnH, 0xFF2E7F3F);
                Framebuffer.Graphics.DrawRectangle(applyBtnX, applyBtnY, applyBtnW, applyBtnH, 0xFF3F3F3F, 1);
                WindowManager.font.DrawString(applyBtnX + 30, applyBtnY + (applyBtnH / 2 - WindowManager.font.FontSize / 2), "Apply");
            }
        }

        public override void Dispose() {
            // Clean up thumbnails - dispose each image, then clear the list
            if (_thumbnails != null) {
                for (int i = 0; i < _thumbnails.Count; i++) {
                    if (_thumbnails[i] != null) {
                        _thumbnails[i].Dispose();
                    }
                }
                _thumbnails.Clear(); // Clear the list instead of calling Dispose on it
            }
            
            // Clean up background paths list
            if (_backgroundPaths != null) {
                _backgroundPaths.Clear(); // Clear the list instead of calling Dispose on it
            }

            // Clean up font paths list
            if (_fontPaths != null) {
                _fontPaths.Clear();
            }
            
            // Clean up resolution labels array
            if (_resLabels != null) {
                for (int i = 0; i < _resLabels.Length; i++) {
                    if (_resLabels[i] != null) {
                        _resLabels[i].Dispose();
                    }
                }
            }
            
            // The open picker is a service-owned transient window.  Cancel
            // its request and let the session table perform window cleanup.
            if (_openRequest.IsValid) {
                if (_serviceContext != null && _services != null &&
                        _services.OpenFile != null) {
                    _services.OpenFile.Cancel(_serviceContext, _openRequest);
                }
                _openRequest = ApplicationServiceRequestHandle.Invalid;
            }
            if (_colorDlg != null && _colorDlg.Visible) {
                _colorDlg.Visible = false;
                _colorDlg.Dispose();
            }
            
            base.Dispose();
        }
    }

    // Simple color picker: 6x6x6 cube palette
    internal class ColorPicker : Window {
        private readonly Action<uint> _onChoose;
        private bool _clickLock;
        private int _padding = 10;
        
        public ColorPicker(int x, int y, Action<uint> onChoose) : base(x, y, 280, 220) { 
            Title = "Choose Color"; 
            _onChoose = onChoose; 
            _clickLock = false; 
        }
        
        public override void OnInput() {
            base.OnInput(); 
            if (!Visible) return;
            
            // Only process input if mouse is within the window bounds
            if (!IsUnderMouse()) return;
            
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left); 
            int mx = Control.MousePosition.X; 
            int my = Control.MousePosition.Y; 
            int cx = X + _padding; 
            int cy = Y + _padding; 
            int sw = 22; 
            int sh = 22; 
            int cols = 12; 
            int rows = 8; 
            
            if (left) { 
                if (!_clickLock) { 
                    for (int r = 0; r < rows; r++) { 
                        for (int c = 0; c < cols; c++) { 
                            int px = cx + c * (sw + 2); 
                            int py = cy + r * (sh + 2); 
                            if (mx >= px && mx <= px + sw && my >= py && my <= py + sh) { 
                                uint color = SampleColor(c, r); 
                                _onChoose?.Invoke(color); 
                                Visible = false; 
                                _clickLock = true; 
                                return; 
                            } 
                        } 
                    } 
                } 
            } else { 
                _clickLock = false; 
            } 
        }
        
        public override void OnDraw() { 
            base.OnDraw(); 
            int cx = X + _padding; 
            int cy = Y + _padding; 
            int sw = 22; 
            int sh = 22; 
            int cols = 12; 
            int rows = 8; 
            
            for (int r = 0; r < rows; r++) { 
                for (int c = 0; c < cols; c++) { 
                    int px = cx + c * (sw + 2); 
                    int py = cy + r * (sh + 2); 
                    uint color = SampleColor(c, r); 
                    Framebuffer.Graphics.FillRectangle(px, py, sw, sh, color); 
                    Framebuffer.Graphics.DrawRectangle(px, py, sw, sh, 0xFF666666, 1);
                } 
            } 
        }
        
        private static uint SampleColor(int c, int r) {
            // Basic palette rows: grayscale + RGB mixes
            if (r == 0) { 
                byte v = (byte)(c * 21); 
                return (uint)(0xFF000000 | v << 16 | v << 8 | v); 
            }
            byte rr = (byte)(c * 21); 
            byte gg = (byte)(r * 30); 
            byte bb = (byte)((c ^ r) * 16);
            return (uint)(0xFF000000 | rr << 16 | gg << 8 | bb);
        }
    }
}
