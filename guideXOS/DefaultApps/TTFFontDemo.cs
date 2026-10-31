using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.FS;
using System;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// TTF Font Demo - Demonstrates TrueType font rendering
    /// </summary>
    internal class TTFFontDemo : Window {
        private TrueTypeFont _ttfFont12;
        private TrueTypeFont _ttfFont20;
        private TrueTypeFont _ttfFont32;
        private bool _fontsLoaded = false;
        private string _errorMessage = "";
        
        public TTFFontDemo(int x, int y, int w, int h) : base(x, y, w, h) {
            this.Title = "TTF Font Demo";
            this.ShowInStartMenu = true;
            
            // Try to load Roboto TTF fonts
            LoadTTFFonts();
        }
        
        private void LoadTTFFonts() {
            try {
                //Console.WriteLine("[TTF Demo] Loading Roboto fonts...");
                
                // Every size is rerasterized from the same packaged outline
                // source. No pre-generated PNG atlas is used for these samples.
                byte[] robotoData = File.ReadAllBytes("Fonts/roboto/Roboto-Regular.ttf");
                
                if (robotoData != null && robotoData.Length > 0) {
                    _ttfFont12 = new TrueTypeFont(robotoData, 12);
                    _ttfFont20 = new TrueTypeFont(robotoData, 20);
                    _ttfFont32 = new TrueTypeFont(robotoData, 32);
                    _fontsLoaded = _ttfFont12.IsValid &&
                                   _ttfFont20.IsValid &&
                                   _ttfFont32.IsValid;
                    if (!_fontsLoaded) {
                        TrueTypeFont failedFont = !_ttfFont12.IsValid ? _ttfFont12 :
                            !_ttfFont20.IsValid ? _ttfFont20 : _ttfFont32;
                        _errorMessage = "Roboto TTF rejected: " + failedFont.LoadFailure;
                    }
                    //Console.WriteLine("[TTF Demo] Successfully loaded Roboto fonts!");
                } else {
                    _errorMessage = "Could not read Roboto-Regular.ttf";
                    //Console.WriteLine("[TTF Demo] Error: " + _errorMessage);
                }
            } catch {
                _errorMessage = "Error loading TTF fonts";
                //Console.WriteLine("[TTF Demo] Error: " + _errorMessage);
            }
        }
        
        public override void OnDraw() {
            base.OnDraw();
            
            int cx = this.X + 1;
            int cy = this.Y + 1;
            int cw = this.Width - 2;
            int ch = this.Height - 2;
            
            // Clear content area
            Framebuffer.Graphics.FillRectangle(cx, cy, cw, ch, 0xFF1E1E1E);
            
            int yOffset = cy + 20;
            
            if (!_fontsLoaded) {
                // Show error message using bitmap font
                WindowManager.font.DrawString(cx + 20, yOffset, "Failed to load TTF fonts!");
                WindowManager.font.DrawString(cx + 20, yOffset + 30, _errorMessage);
                WindowManager.font.DrawString(cx + 20, yOffset + 60, "Make sure Roboto-Regular.ttf");
                WindowManager.font.DrawString(cx + 20, yOffset + 90, "is in Fonts/roboto/ directory");
                return;
            }
            
            // Title using bitmap font
            WindowManager.font.DrawString(cx + 20, yOffset, "TrueType Font Rendering Test");
            yOffset += 28;
            
            try {
                WindowManager.font.DrawString(cx + 20, yOffset, "12 px Roboto Regular");
                yOffset += 19;
                _ttfFont12.DrawString(Framebuffer.Graphics, cx + 32, yOffset,
                    "AaZz 0123456789 !@#$%&*()?,.", 0xFFF5F5F5);
                yOffset += 28;

                WindowManager.font.DrawString(cx + 20, yOffset, "20 px Roboto Regular");
                yOffset += 19;
                _ttfFont20.DrawString(Framebuffer.Graphics, cx + 32, yOffset,
                    "The quick brown fox jumps over the lazy dog.", 0xFFFFFFFF);
                yOffset += 32;

                WindowManager.font.DrawString(cx + 20, yOffset, "32 px Roboto Regular");
                yOffset += 19;
                _ttfFont32.DrawString(Framebuffer.Graphics, cx + 32, yOffset,
                    "TrueType outlines", 0xFFFFFFFF);
                yOffset += 43;

                WindowManager.font.DrawString(cx + 20, yOffset, "Unicode: café, Ω, £");
                _ttfFont20.DrawString(Framebuffer.Graphics, cx + 32, yOffset + 18,
                    "café Ω £", 0xFFFFFFFF);
                yOffset += 54;

                WindowManager.font.DrawString(cx + 20, yOffset, "Repeated redraws use the glyph cache");
                _ttfFont12.DrawString(Framebuffer.Graphics, cx + cw - 170, yOffset + 18,
                    "right-edge clipping sample", 0xFFFFFFFF);
                
            } catch {
                WindowManager.font.DrawString(cx + 20, yOffset, "Rendering error occurred");
            }
        }
        
        public override void OnInput() {
            base.OnInput();
        }
    }
}
