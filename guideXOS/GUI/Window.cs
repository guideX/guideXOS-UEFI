using guideXOS.Enum;
using guideXOS.Graph;
using guideXOS.GUI.Base;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using System;
using System.Drawing;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Window
    /// </summary>
    abstract class Window : WindowBase {
        /// <summary>
        /// Index
        /// </summary>
        public int Index { get => WindowManager.Windows.IndexOf(this); }
        /// <summary>
        /// Minimized or maximized state
        /// </summary>
        public bool IsMinimized { get; private set; }
        /// <summary>
        /// Is Maximized
        /// </summary>
        public bool IsMaximized { get; private set; }
        /// <summary>
        /// Added: tombstone state (freeze UI until restored)
        /// </summary>
        public bool IsTombstoned { get; private set; }
        /// <summary>
        /// Semantic owner.  WindowManager remains the graphical owner; this
        /// handle links the window to its application instance.
        /// </summary>
        internal ApplicationInstanceHandle ApplicationInstanceHandle { get; private set; }
        #region "private variables"
        /// <summary>
        /// Owner ID
        /// </summary>
        private int _ownerId; public int OwnerId => _ownerId;
        /// <summary>
        /// Title buttons state and layout
        /// </summary>
        private enum TitleButton { None, Minimize, Maximize, Restore, Tombstone, Close }
        private TitleButton _hoverBtn = TitleButton.None;
        private TitleButton _pressedBtn = TitleButton.None;
        private bool _captureButtons = false;
        private int _btnSize, _btnY, _btnH;
        private int _rcCloseX, _rcTombX, _rcRestoreX, _rcMaxX, _rcMinX;
        private const int _btnSpacing = 6;
        /// <summary>
        /// Remember normal bounds for restore
        /// </summary>
        private int _normX, _normY, _normW, _normH;
        /// <summary>
        /// Move
        /// </summary>
        private bool Move;
        /// <summary>
        /// Offset X
        /// </summary>
        private int OffsetX;
        /// <summary>
        /// Offset Y
        /// </summary>
        private int OffsetY;
        /// <summary>
        /// Animation Type
        /// </summary>
        private WindowAnimationTypeEnum _animType = WindowAnimationTypeEnum.FadeIn;
        /// <summary>
        /// Animation Start Ticks
        /// </summary>
        private ulong _animStartTicks;
        /// <summary>
        /// Animation Duration Ms
        /// </summary>
        private int _animDurationMs;
        /// <summary>
        /// Animation Start Y
        /// </summary>
        private int _animStartY;
        /// <summary>
        /// Animation End Y
        /// </summary>
        private int _animEndY;
        /// <summary>
        /// Overlay Alpha for fade
        /// </summary>
        private byte _overlayAlpha; // 0..255
        /// <summary>
        /// Is Animating
        /// </summary>
        private bool IsAnimating => _animType != WindowAnimationTypeEnum.None;
        /// <summary>
        /// Special effect type for open/close
        /// </summary>
        private WindowEffectType _currentEffect = WindowEffectType.None;
        /// <summary>
        /// Special effect progress (0.0 to 1.0)
        /// </summary>
        private float _effectProgress = 0f;
        /// <summary>
        /// Resizing
        /// </summary>
        private bool _resizing;
        private int _resizeStartMouseX, _resizeStartMouseY;
        private int _resizeStartW, _resizeStartH;
        private const int _resizeGripSize = 16;
        /// <summary>
        /// Cached blurred title bar to prevent memory leak from repeated BlurRectangle calls
        /// </summary>
        private Image _blurredBarCache;
        private Graphics _blurredBarG; // persistent graphics for cache to avoid repeated allocations
        private int _cachedBarX, _cachedBarY, _cachedBarW, _cachedBarH;
        private bool _disposed;
        #endregion
        #region "methods"
        /// <summary>
        /// Visible
        /// </summary>
        public bool Visible {
            set {
                // trigger fade animations on show/hide
                if (value && !_visible) { 
                    _visible = true; 
                    BeginFadeIn(); 
                    OnSetVisible(true); 
                    return; 
                }
                if (!value && _visible) { 
                    BeginFadeOutClose(); 
                    OnSetVisible(false); 
                    return; 
                }
                _visible = value;
                OnSetVisible(value);
            }
            get { 
                return _visible; 
            }
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="Width"></param>
        /// <param name="Height"></param>
        public Window(int X, int Y, int Width, int Height) {
            this.X = X; this.Y = Y; this.Width = Width; this.Height = Height; _normX = X; _normY = Y; _normW = Width; _normH = Height;
            ClampToScreen(); 
            this.Visible = true; 
            WindowManager.Windows.Add(this); 
            Title = "Window1"; 
            TaskbarIcon = Icons.DocumentIcon(32);
            _ownerId = WindowManager.Windows.IndexOf(this) + 1; // stable owner id
            Allocator.CurrentOwnerId = _ownerId; // set current owner context during construction allocations
            BeginFadeIn();
            Allocator.CurrentOwnerId = 0; // reset
        }
        /// <summary>
        /// Begin Fade In
        /// </summary>
        void BeginFadeIn() {
            // Check if special effects are enabled
            if (UISettings.EnableSpecialWindowEffects) {
                WindowEffectType effect = UISettings.WindowOpenEffect;
                if (effect == WindowEffectType.Random) {
                    effect = SpecialEffects.GetRandomEffect();
                }
                if (effect != WindowEffectType.None && effect != WindowEffectType.Fade) {
                    _currentEffect = effect;
                    _animType = WindowAnimationTypeEnum.FadeIn;
                    _animStartTicks = Timer.Ticks;
                    _animDurationMs = UISettings.SpecialEffectDurationMs;
                    _effectProgress = 0f;
                    return;
                }
            }
            
            // Fall back to regular fade animation
            if (!UISettings.EnableFadeAnimations) return;
            _animType = WindowAnimationTypeEnum.FadeIn;
            _animStartTicks = Timer.Ticks;
            _animDurationMs = UISettings.FadeInDurationMs;
            _overlayAlpha = 255;
        }
        /// <summary>
        /// Begin Fade Out Close
        /// </summary>
        void BeginFadeOutClose() {
            // Check if special effects are enabled
            if (UISettings.EnableSpecialWindowEffects) {
                WindowEffectType effect = UISettings.WindowCloseEffect;
                if (effect == WindowEffectType.Random) {
                    effect = SpecialEffects.GetRandomEffect();
                }
                if (effect != WindowEffectType.None && effect != WindowEffectType.Fade) {
                    _currentEffect = effect;
                    _animType = WindowAnimationTypeEnum.FadeOutClose;
                    _animStartTicks = Timer.Ticks;
                    _animDurationMs = UISettings.SpecialEffectDurationMs;
                    _effectProgress = 0f;
                    return;
                }
            }
            
            // Fall back to regular fade animation
            if (!UISettings.EnableFadeAnimations) { 
                this._visible = false;
                // CRITICAL: Dispose window even when animations are disabled
                Dispose();
                return;
            }
            _animType = WindowAnimationTypeEnum.FadeOutClose;
            _animStartTicks = Timer.Ticks;
            _animDurationMs = UISettings.FadeOutDurationMs;
            _overlayAlpha = 0;
        }
        /// <summary>
        /// Begin Minimize
        /// </summary>
        void BeginMinimize() {
            if (!UISettings.EnableWindowSlideAnimations) { 
                IsMinimized = true; 
                // Note: Minimize doesn't dispose, just hides
                return;
            }
            _animType = WindowAnimationTypeEnum.Minimize;
            _animStartTicks = Timer.Ticks;
            _animDurationMs = UISettings.WindowSlideDurationMs;
            _animStartY = Y;
            _animEndY = Framebuffer.Height + Height; // slide below screen
        }
        /// <summary>
        /// Begin Restore
        /// </summary>
        void BeginRestore() {
            if (!UISettings.EnableWindowSlideAnimations) { Y = _normY; IsMinimized = false; return; }
            // start from off-screen bottom to normal Y
            _animType = WindowAnimationTypeEnum.Restore;
            _animStartTicks = Timer.Ticks;
            _animDurationMs = UISettings.WindowSlideDurationMs;
            Y = Framebuffer.Height + Height;
            _animStartY = Y;
            _animEndY = _normY;
        }
        /// <summary>
        /// Update Animation
        /// </summary>
        void UpdateAnimation() {
            if (!UISettings.UpdateAnimations) return;
            if (!IsAnimating) return;
            // compute progress 0..1 based on Timer.Ticks (assumed ms-scale)
            ulong elapsed = (Timer.Ticks > _animStartTicks) ? (Timer.Ticks - _animStartTicks) : 0UL;
            float t = _animDurationMs > 0 ? (float)elapsed / _animDurationMs : 1f;
            if (t > 1f) t = 1f;

            switch (_animType) {
                case WindowAnimationTypeEnum.FadeIn: {
                        // Check if using special effect
                        if (_currentEffect != WindowEffectType.None && _currentEffect != WindowEffectType.Fade) {
                            _effectProgress = t;
                            if (t >= 1f) { 
                                _animType = WindowAnimationTypeEnum.None; 
                                _currentEffect = WindowEffectType.None;
                                _effectProgress = 0f;
                            }
                        } else {
                            _overlayAlpha = (byte)(255 - (int)(t * 255f));
                            if (t >= 1f) { _overlayAlpha = 0; _animType = WindowAnimationTypeEnum.None; }
                        }
                        break;
                    }
                case WindowAnimationTypeEnum.FadeOutClose: {
                        // Check if using special effect
                        if (_currentEffect != WindowEffectType.None && _currentEffect != WindowEffectType.Fade) {
                            _effectProgress = t;
                            if (t >= 1f) {
                                _animType = WindowAnimationTypeEnum.None; 
                                _currentEffect = WindowEffectType.None;
                                _effectProgress = 0f;
                                this._visible = false;
                                Dispose();
                            }
                        } else {
                            _overlayAlpha = (byte)((int)(t * 255f));
                            if (t >= 1f) {
                                _overlayAlpha = 0; 
                                _animType = WindowAnimationTypeEnum.None; 
                                this._visible = false; // hide after fade
                                
                                // CRITICAL: Dispose window resources after fade-out completes
                                // This ensures memory is freed when window closes
                                Dispose();
                            }
                        }
                        break;
                    }
                case WindowAnimationTypeEnum.Minimize: {
                        int ny = _animStartY + (int)((_animEndY - _animStartY) * t);
                        Y = ny;
                        if (t >= 1f) { IsMinimized = true; _animType = WindowAnimationTypeEnum.None; Y = _animEndY; }
                        break;
                    }
                case WindowAnimationTypeEnum.Restore: {
                        int ny = _animStartY + (int)((_animEndY - _animStartY) * t);
                        Y = ny;
                        if (t >= 1f) { Y = _normY; _animType = WindowAnimationTypeEnum.None; }
                        break;
                    }
            }
        }
        /// <summary>
        /// Is Under Mouse
        /// </summary>
        /// <returns></returns>
        public bool IsUnderMouse() {
            // Include title bar area in hit test
            if (Control.MousePosition.X > X &&
                Control.MousePosition.X < X + Width &&
                Control.MousePosition.Y > Y - BarHeight &&
                Control.MousePosition.Y < Y + Height) return true;
            return false;
        }
        /// <summary>
        /// On Set Visible
        /// </summary>
        /// <param name="value"></param>
        public virtual void OnSetVisible(bool value) { 
            // Dispose blur cache when hiding to free memory immediately
            if (!value) { DisposeBlurCache(); }
        }
        private void DisposeBlurCache(){
            if(_blurredBarG!=null){ _blurredBarG.ResetBlurBuffers(); _blurredBarG.Dispose(); _blurredBarG=null; }
            if(_blurredBarCache!=null){ _blurredBarCache.Dispose(); _blurredBarCache=null; }
        }
        void ComputeButtonRects() {
            _btnSize = BarHeight - 12; if (_btnSize < 16) _btnSize = 16; _btnH = _btnSize; _btnY = Y - BarHeight + (BarHeight - _btnSize) / 2;
            
            // Position buttons from right to left, only for visible buttons
            int right = X + Width - 8;
            int currentX = right - _btnSize;
            
            // Close button always present
            _rcCloseX = currentX;
            currentX -= (_btnSize + _btnSpacing);
            
            // Add other buttons only if they're shown
            if (ShowTombstone) {
                _rcTombX = currentX;
                currentX -= (_btnSize + _btnSpacing);
            } else {
                _rcTombX = -1000; // off-screen
            }
            
            // Combined Maximize/Restore button - show restore when maximized, maximize when not
            if (ShowMaximize) {
                if (IsMaximized) {
                    _rcRestoreX = currentX;
                    _rcMaxX = -1000; // off-screen
                } else {
                    _rcMaxX = currentX;
                    _rcRestoreX = -1000; // off-screen
                }
                currentX -= (_btnSize + _btnSpacing);
            } else {
                _rcRestoreX = -1000; // off-screen
                _rcMaxX = -1000; // off-screen
            }
            
            if (ShowMinimize) {
                _rcMinX = currentX;
            } else {
                _rcMinX = -1000; // off-screen
            }
        }

        bool Hit(int mx, int my, int x, int y, int w, int h) => (mx >= x && mx <= x + w && my >= y && my <= y + h);

        TitleButton HitTestButtons(int mx, int my) {
            if (Hit(mx, my, _rcCloseX, _btnY, _btnSize, _btnH)) return TitleButton.Close;
            if (ShowTombstone && Hit(mx, my, _rcTombX, _btnY, _btnSize, _btnH)) return TitleButton.Tombstone;
            // Combined button hit test - check both positions
            if (ShowMaximize) {
                if (IsMaximized && Hit(mx, my, _rcRestoreX, _btnY, _btnSize, _btnH)) return TitleButton.Restore;
                if (!IsMaximized && Hit(mx, my, _rcMaxX, _btnY, _btnSize, _btnH)) return TitleButton.Maximize;
            }
            if (ShowMinimize && Hit(mx, my, _rcMinX, _btnY, _btnSize, _btnH)) return TitleButton.Minimize;
            return TitleButton.None;
        }
        /// <summary>
        /// Perform Button
        /// </summary>
        /// <param name="b"></param>
        void PerformButton(TitleButton b) {
            switch (b) {
                case TitleButton.Close: BeginFadeOutClose(); break;
                case TitleButton.Minimize: if (ShowMinimize) Minimize(); break;
                case TitleButton.Maximize: if (ShowMaximize) Maximize(); break;
                case TitleButton.Restore: if (ShowMaximize) { Restore(); if (IsTombstoned) { IsTombstoned = false; this.Visible = true; } } break;
                case TitleButton.Tombstone: if (ShowTombstone) { IsTombstoned = true; this.Visible = false; } break;
            }
        }

        void DrawTitleButton(int x, int y, int sz, TitleButton type, bool hover, bool pressed) {
            // Only draw button if button backgrounds or borders are enabled
            if (UISettings.EnableButtonBackgrounds || UISettings.EnableButtonBorders) {
                uint baseFill = pressed ? 0xFF2A2A2Au : (hover ? 0xFF343434u : 0xFF2E2E2Eu);
                uint border = 0xFF505050u;
                
                // Glow halo on hover (only if hover effects enabled)
                if (hover && UISettings.EnableButtonHoverEffects) {
                    UIPrimitives.AFillRoundedRect(x - 2, y - 2, sz + 4, sz + 4, 0x332E89FF, 6);
                }
                
                // Use rounded corners based on setting
                int cornerRadius = UISettings.EnableRoundedCorners ? 6 : 0;
                
                // Draw button background (only if enabled)
                if (UISettings.EnableButtonBackgrounds) {
                    UIPrimitives.AFillRoundedRect(x, y, sz, sz, baseFill, cornerRadius);
                }
                
                // Draw button border (only if enabled)
                if (UISettings.EnableButtonBorders) {
                    UIPrimitives.DrawRoundedRect(x, y, sz, sz, border, 1, cornerRadius);
                }
            }

            // Draw button icons (only if enabled)
            if (UISettings.EnableButtonIcons) {
                uint fg = pressed ? 0xFFEEEEEEu : 0xFFFAFAFAu;
                int cx = x + sz / 2; int cy = y + sz / 2; int lw = 2;
                switch (type) {
                    case TitleButton.Close: {
                            int r = sz / 3;
                            // approximate thickness by drawing two lines
                            Framebuffer.Graphics.DrawLine(cx - r, cy - r, cx + r, cy + r, fg);
                            Framebuffer.Graphics.DrawLine(cx - r + 1, cy - r, cx + r + 1, cy + r, fg);
                            Framebuffer.Graphics.DrawLine(cx + r, cy - r, cx - r, cy + r, fg);
                            Framebuffer.Graphics.DrawLine(cx + r + 1, cy - r, cx - r + 1, cy + r, fg);
                            break;
                        }
                    case TitleButton.Minimize: {
                            int w = sz / 2; int hx = cx - w / 2; int hy = y + sz - 8;
                            Framebuffer.Graphics.FillRectangle(hx, hy, w, lw, fg);
                            break;
                        }
                    case TitleButton.Maximize: {
                            int s = sz / 2; int rx = cx - s / 2; int ry = cy - s / 2;
                            Framebuffer.Graphics.DrawRectangle(rx, ry, s, s, fg, lw);
                            break;
                        }
                    case TitleButton.Restore: {
                            int s = sz / 2; int rx1 = cx - s / 2 + 3; int ry1 = cy - s / 2; int rx2 = cx - s / 2; int ry2 = cy - s / 2 + 3;
                            Framebuffer.Graphics.DrawRectangle(rx1, ry1, s, s, fg, 1);
                            Framebuffer.Graphics.DrawRectangle(rx2, ry2, s, s, fg, 1);
                            break;
                        }
                    case TitleButton.Tombstone: {
                            int w = sz / 2; int rx = cx - w / 2; int ry = cy - w / 2;
                            UIPrimitives.DrawRoundedRect(rx, ry, w, w + 3, fg, 1, 6);
                            Framebuffer.Graphics.FillRectangle(rx - 2, ry + w + 4, w + 4, 2, fg);
                            break;
                        }
                }
            }
        }
        /// <summary>
        /// On Input
        /// </summary>
        public virtual void OnInput() {
            if (!Visible) return;
            if (IsMinimized) return;
            if (_animType != WindowAnimationTypeEnum.None) return;

            ComputeButtonRects();
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            _hoverBtn = HitTestButtons(mx, my);
            bool left = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;

            // Title buttons interaction
            if (left) {
                if (!_captureButtons && _hoverBtn != TitleButton.None) { _pressedBtn = _hoverBtn; _captureButtons = true; return; }
            } else {
                if (_captureButtons) {
                    if (_hoverBtn == _pressedBtn) PerformButton(_pressedBtn);
                    _pressedBtn = TitleButton.None; _captureButtons = false; return;
                }
            }

            // Block other input when tombstoned
            if (IsTombstoned) return;

            // Drag title bar (skip when capturing title button clicks to prevent drag interference)
            if (left && !_captureButtons) {
                if (!WindowManager.HasWindowMoving && !Move && mx > X && mx < X + Width && my > Y - BarHeight && my < Y) {
                    WindowManager.MoveToEnd(this);
                    Move = true; WindowManager.HasWindowMoving = true; OffsetX = mx - X; OffsetY = my - Y;
                }
            } else if (!left) { Move = false; WindowManager.HasWindowMoving = false; }

            if (Move) {
                X = mx - OffsetX; Y = my - OffsetY; ClampToScreen(); _normX = X; _normY = Y; _normW = Width; _normH = Height;
                // Invalidate blur cache when window moves
                DisposeBlurCache();
            }

            // Resize handling in bottom-right corner
            if (IsResizable) {
                int gripX = X + Width - _resizeGripSize; int gripY = Y + Height - _resizeGripSize;
                bool over = mx >= gripX && mx <= gripX + _resizeGripSize && my >= gripY && my <= gripY + _resizeGripSize;
                if (left) {
                    if (!_resizing && over) {
                        _resizing = true; _resizeStartMouseX = mx; _resizeStartMouseY = my; _resizeStartW = Width; _resizeStartH = Height;
                        return;
                    }
                } else {
                    _resizing = false;
                }
                if (_resizing) {
                    int dw = mx - _resizeStartMouseX; int dh = my - _resizeStartMouseY;
                    int newW = _resizeStartW + dw; int newH = _resizeStartH + dh;
                    if (newW < 160) newW = 160; if (newH < 120) newH = 120;
                    Width = newW; Height = newH;
                    // Invalidate blur cache when window resizes
                    DisposeBlurCache();
                    return;
                }
            }
        }
        /// <summary>
        /// On Draw
        /// </summary>
        public virtual void OnDraw() {
            if (!Visible) return;
            if (IsMinimized && _animType != WindowAnimationTypeEnum.Restore) return;
            if (Framebuffer.Graphics == null || WindowManager.font == null) return;

            UpdateAnimation();

            // Soft glow behind window (only if transparency and glow enabled)
            if (UISettings.EnableTransparentWindows && UISettings.EnableWindowGlow) {
                int glowPad = 10; // radius for glow halo
                int glowRadius = UISettings.EnableRoundedCorners ? 10 : 0;
                UIPrimitives.AFillRoundedRect(X - glowPad, Y - BarHeight - glowPad, Width + glowPad * 2, Height + BarHeight + glowPad * 2, 0x221E90FF, glowRadius);
            }

            int barX = X; int barY = Y - BarHeight; int barW = Width; int barH = BarHeight;
            int cornerRadius = UISettings.EnableRoundedCorners ? 4 : 0;
            
            // Only draw title bar background if enabled
            if (UISettings.EnableTitleBarBackground) {
                // Only apply blur if enabled in UISettings
                if (UISettings.EnableBlurredTitleBars) {
                    // Only refresh if cache not built yet or geometry changed
                    bool needsRefresh = _blurredBarCache == null || barX != _cachedBarX || barY != _cachedBarY || barW != _cachedBarW || barH != _cachedBarH;
                    
                    if (needsRefresh) {
                        DisposeBlurCache();
                        _blurredBarCache = new Image(barW, barH);
                        var blurredBarG = Graphics.FromImage(_blurredBarCache);
                        // Copy region
                        for (int yy = 0; yy < barH; yy++) {
                            for (int xx = 0; xx < barW; xx++) {
                                int sx = barX + xx; int sy = barY + yy;
                                if (sx >= 0 && sy >= 0 && sx < Framebuffer.Width && sy < Framebuffer.Height) {
                                    _blurredBarCache.RawData[yy * barW + xx] = (int)Framebuffer.Graphics.GetPoint(sx, sy);
                                }
                            }
                        }
                        // Blur using persistent graphics buffers
                        blurredBarG.BlurRectangle(0, 0, barW, barH, 3);
                        blurredBarG.Dispose();
                        _cachedBarX = barX; _cachedBarY = barY; _cachedBarW = barW; _cachedBarH = barH;
                    }
                    
                    // Draw the cached blurred bar
                    if (_blurredBarCache != null) {
                        Framebuffer.Graphics.DrawImage(barX, barY, _blurredBarCache, false);
                    }
                    
                    // Apply transparency overlay if enabled
                    if (UISettings.EnableTransparentWindows) {
                        UIPrimitives.AFillRoundedRectTop(barX, barY, barW, barH, 0x66111111, cornerRadius);
                    } else {
                        UIPrimitives.AFillRoundedRectTop(barX, barY, barW, barH, 0xAA111111, cornerRadius);
                    }
                } else {
                    // Simple solid title bar when blur is disabled
                    UIPrimitives.AFillRoundedRectTop(barX, barY, barW, barH, 0xFF111111, cornerRadius);
                }
            }

            // Draw window title (only if text rendering and window titles enabled)
            if (UISettings.EnableTextRendering && UISettings.EnableWindowTitles) {
                bool titleIsEmpty = string.IsNullOrEmpty(Title);
                if (!titleIsEmpty) {
                    int measured = WindowManager.font.MeasureString(Title);
                    int tx = X + (Width / 2) - (measured / 2);
                    int ty = Y - BarHeight + (BarHeight / 4);
                    WindowManager.font.DrawString(tx, ty, Title);
                } else {
                    // Only allocate string.Empty if needed
                    string title = string.Empty;
                    int measured = WindowManager.font.MeasureString(title);
                    int tx = X + (Width / 2) - (measured / 2);
                    int ty = Y - BarHeight + (BarHeight / 4);
                    WindowManager.font.DrawString(tx, ty, title);
                }
            }

            // Custom title buttons with glow and press states (only if enabled)
            if (UISettings.EnableTitleBarButtons) {
                ComputeButtonRects();
                if (ShowMinimize) DrawTitleButton(_rcMinX, _btnY, _btnSize, TitleButton.Minimize, _hoverBtn == TitleButton.Minimize, _pressedBtn == TitleButton.Minimize && _captureButtons);
                // Combined Maximize/Restore button - show appropriate one based on state
                if (ShowMaximize) {
                    if (IsMaximized) {
                        DrawTitleButton(_rcRestoreX, _btnY, _btnSize, TitleButton.Restore, _hoverBtn == TitleButton.Restore, _pressedBtn == TitleButton.Restore && _captureButtons);
                    } else {
                        DrawTitleButton(_rcMaxX, _btnY, _btnSize, TitleButton.Maximize, _hoverBtn == TitleButton.Maximize, _pressedBtn == TitleButton.Maximize && _captureButtons);
                    }
                }
                if (ShowTombstone) DrawTitleButton(_rcTombX, _btnY, _btnSize, TitleButton.Tombstone, _hoverBtn == TitleButton.Tombstone, _pressedBtn == TitleButton.Tombstone && _captureButtons);
                // Close button always present
                DrawTitleButton(_rcCloseX, _btnY, _btnSize, TitleButton.Close, _hoverBtn == TitleButton.Close, _pressedBtn == TitleButton.Close && _captureButtons);
            }

            // Content - use solid color when transparency is disabled (only if enabled)
            if (UISettings.EnableWindowContentBackground) {
                uint contentColor = UISettings.EnableTransparentWindows ? 0xCC222222u : 0xFF222222u;
                UIPrimitives.AFillRoundedRect(X, Y, Width, Height, contentColor, cornerRadius);
            }
            
            // Draw border (only if enabled)
            if (UISettings.EnableWindowBorders) {
                DrawBorder();
            }

            // Tombstone overlay (only if enabled)
            if (IsTombstoned && UISettings.EnableTombstoneOverlay) {
                uint tombstoneColor = UISettings.EnableTransparentWindows ? 0x88111111u : 0xDD111111u;
                UIPrimitives.AFillRoundedRect(X, Y, Width, Height, tombstoneColor, cornerRadius);
                
                // Tombstone text (only if text rendering and tombstone text enabled)
                if (UISettings.EnableTextRendering && UISettings.EnableTombstoneText) {
                    string msg = "Tombstoned"; 
                    int tw = WindowManager.font.MeasureString(msg);
                    WindowManager.font.DrawString(X + (Width - tw) / 2, Y + (Height - WindowManager.font.FontSize) / 2, msg);
                }
            }

            // Special effect overlay
            if (_currentEffect != WindowEffectType.None && _currentEffect != WindowEffectType.Fade && IsAnimating) {
                try {
                    SpecialEffects.RenderEffect(_currentEffect, X, Y, Width, Height, BarHeight, _effectProgress);
                } catch {
                    // If effects fail (e.g., early boot/AOT init), disable and continue to avoid panic
                    _currentEffect = WindowEffectType.None;
                }
            }
            
            // Fade overlay (for regular fade animations)
            if (_overlayAlpha > 0 && _animType != WindowAnimationTypeEnum.None && (_animType == WindowAnimationTypeEnum.FadeIn || _animType == WindowAnimationTypeEnum.FadeOutClose) && _currentEffect == WindowEffectType.None) {
                uint col = (uint)(_overlayAlpha) << 24;
                UIPrimitives.AFillRoundedRect(X - 1, Y - BarHeight - 1, Width + 2, Height + BarHeight + 2, col, cornerRadius);
            }

            // Resize grip visual (only if enabled)
            if (IsResizable && UISettings.EnableResizeGrip) {
                // Draw at bottom-right corner
                int gx = X + Width - _resizeGripSize; 
                int gy = Y + Height - _resizeGripSize;
                
                uint gripColor = UISettings.EnableTransparentWindows ? 0x332F2F2Fu : 0xFF2F2F2Fu;
                Framebuffer.Graphics.FillRectangle(gx, gy, _resizeGripSize, _resizeGripSize, gripColor);
                
                // three little diagonal lines
                int inset = 4; 
                uint lc = 0xFF777777;
                for (int i = 0; i < 3; i++) {
                    int ox = gx + _resizeGripSize - inset - (i * 4); 
                    int oy = gy + _resizeGripSize - inset;
                    Framebuffer.Graphics.DrawLine(ox - 6, oy, ox, oy - 6, lc);
                }
                
                int gripCornerRadius = UISettings.EnableRoundedCorners ? 2 : 0;
                UIPrimitives.DrawRoundedRect(gx, gy, _resizeGripSize, _resizeGripSize, 0xFF444444, 1, gripCornerRadius);
            }
        }
        /// <summary>
        /// Draw Border
        /// </summary>
        /// <param name="HasBar"></param>
        public void DrawBorder(bool HasBar = true) {
            if (Framebuffer.Graphics == null) return;
            int cornerRadius = UISettings.EnableRoundedCorners ? 4 : 0;
            UIPrimitives.DrawRoundedRect(X - 1, Y - (HasBar ? BarHeight : 0) - 1, Width + 2, (HasBar ? BarHeight : 0) + Height + 2, 0xFF333333, 1, cornerRadius);
        }
        /// <summary>
        /// Clamp To Screen
        /// </summary>
        private void ClampToScreen() {
            int maxX = Framebuffer.Width - Width;
            if (X > maxX) X = maxX;
            if (X < 0) X = 0;
            int maxY = Framebuffer.Height - Height;
            if (Y > maxY) Y = maxY;
            if (Y < BarHeight) Y = BarHeight;
        }
        /// <summary>
        /// Minimize window
        /// </summary>
        public void Minimize() {
            if (IsMinimized) return;
            if (_animType != WindowAnimationTypeEnum.None) return;
            BeginMinimize();
        }
        /// <summary>
        /// Restore from minimized or maximized
        /// </summary>
        public void Restore() {
            if (!IsMinimized && !IsMaximized) return;
            if (_animType != WindowAnimationTypeEnum.None) return;
            IsMinimized = false; IsMaximized = false;
            BeginRestore();
        }
        /// <summary>
        /// Maximize to full screen area (minus taskbar)
        /// </summary>
        public void Maximize() {
            if (IsMaximized) return;
            if (_animType != WindowAnimationTypeEnum.None) return;
            // remember
            _normX = X; _normY = Y; _normW = Width; _normH = Height;
            IsMaximized = true; IsMinimized = false;
            X = 0; Y = BarHeight; Width = Framebuffer.Width; Height = Framebuffer.Height - (BarHeight + WindowManagerMinBar());
        }
        private int WindowManagerMinBar() { return 40; } // taskbar height
        /// <summary>
        /// Tombstone (freeze/disable) the window until restored
        /// </summary>
        public void Tombstone() { IsTombstoned = true; this.Visible = false; }
        /// <summary>
        /// Remove tombstone (enable input)
        /// </summary>
        public void Untombstone() { IsTombstoned = false; this.Visible = true; }
        /// <summary>
        /// On Global Key
        /// </summary>
        /// <param name="key"></param>
        public virtual void OnGlobalKey(ConsoleKeyInfo key){ 
            // FIXED: Removed debug WriteLine that was outputting key information to console
            // This was causing "Draw Start" and "34" to appear in FConsole
            if(key.Key==System.ConsoleKey.Escape){ // close on escape if visible and allowed
                if(this.Visible && !this.IsTombstoned){
                    this.Visible=false;
                }
            }
        }
        
        /// <summary>
        /// Dispose window and free all associated memory
        /// </summary>
        public virtual new void Dispose() {
            if (_disposed) return;
            _disposed = true;
            ApplicationInstanceRegistry.OnWindowClosed(this);
            _visible = false;
            IsMinimized = false;
            IsTombstoned = false;
            // Dispose blur cache
            DisposeBlurCache();
            
            // Free all memory owned by this window via the allocator
            if (_ownerId != 0) {
                FreeOwnerMemory(_ownerId);
            }
        }
        
        /// <summary>
        /// Free all memory allocated by a specific owner (window)
        /// </summary>
        private unsafe void FreeOwnerMemory(int ownerId) {
            if (ownerId == 0)
                return;

            try {
                // Scan all pages and free runs owned by this ownerId
                fixed (Allocator.Info* pInfo = &Allocator._Info) {
                    for (int i = 0; i < Allocator.NumPages;) {
                        ulong run = pInfo->Pages[i];
                        if (run != 0 && run != Allocator.PageSignature) {
                            // This is a run start - check if owned by ownerId
                            if (pInfo->Owners[i] == ownerId) {
                                // Free this allocation
                                long baseAddr = (long)pInfo->Start;
                                long offset = (long)(i * Allocator.PageSize);
                                IntPtr ptr = new IntPtr((void*)(baseAddr + offset));
                                Allocator.Free(ptr);
                                // Don't increment i - the Free() cleared the Pages[] entries
                                continue;
                            }
                            // Skip ahead by run length
                            i += (int)run;
                        } else {
                            i++;
                        }
                    }
                }
            } catch {
                // If memory cleanup fails, at least we tried
            }
        }

        internal void SetApplicationInstance(ApplicationInstanceHandle handle) {
            ApplicationInstanceHandle = handle;
        }

        internal void ClearApplicationInstance() {
            ApplicationInstanceHandle = ApplicationInstanceHandle.None;
        }

        /// <summary>
        /// Close a window as part of semantic application termination.  The
        /// manager will perform the existing disposal pass on the next frame.
        /// </summary>
        internal void CloseForApplicationTermination() {
            _animType = WindowAnimationTypeEnum.None;
            _currentEffect = WindowEffectType.None;
            _overlayAlpha = 0;
            IsMinimized = false;
            IsTombstoned = false;
            _visible = false;
        }
        #endregion
    }
}
