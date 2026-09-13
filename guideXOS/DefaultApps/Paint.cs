using guideXOS.Graph;
using guideXOS.GUI;
using guideXOS.GUI.Widgets;
using guideXOS.Kernel.Drivers;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Paint Tool Types
    /// </summary>
    public enum PaintTool {
        Pencil,
        Brush,
        Eraser,
        Line,
        Rectangle,
        Circle,
        FillBucket
    }
    
    internal unsafe class Paint : Window {
        Image img;
        Graphics g;
        
        // Tool state
        private PaintTool _currentTool = PaintTool.Brush;
        private int _brushSize = 5;
        
        // Drawing state
        private int LastX, LastY;
        private uint CurrentColor;
        private uint BackgroundColor = 0xFF222222;
        
        // Tool preview state (for line, rectangle, circle)
        private bool _isDrawing = false;
        private int _startX, _startY;
        
        // UI buttons
        private List<Button> ColorBtns;
        private List<Button> ToolBtns;
        private List<Button> SizeBtns;
        
        // Maps to store button data
        private Dictionary<Button, PaintTool> _toolMap;
        private Dictionary<Button, int> _sizeMap;
        
        private const int ToolbarHeight = 80;
        private const int ColorBarHeight = 40;
        
        // Error state
        private string _lastError = null;
        
        public Paint(int X, int Y) : base(X, Y, 800, 600) {
            try {
                IsResizable = true;
                ShowInTaskbar = true;
                ShowMaximize = true;
                ShowMinimize = true;
                ShowTombstone = true;
                Title = "Paint";
                
                _toolMap = new Dictionary<Button, PaintTool>();
                _sizeMap = new Dictionary<Button, int>();
                
                // Initialize color buttons
                ColorBtns = new List<Button>();
                AddColorButton(10, 10, 0xFFC0392B);
                AddColorButton(40, 10, 0xFFE74C3C);
                AddColorButton(70, 10, 0xFFAF7AC5);
                AddColorButton(100, 10, 0xFF8E44AD);
                AddColorButton(130, 10, 0xFF2980B9);
                AddColorButton(160, 10, 0xFF5DADE2);
                AddColorButton(190, 10, 0xFF1ABC9C);
                AddColorButton(220, 10, 0xFF45B39D);
                AddColorButton(250, 10, 0xFF52BE80);
                AddColorButton(280, 10, 0xFF27AE60);
                AddColorButton(310, 10, 0xFFF1C40F);
                AddColorButton(340, 10, 0xFFE67E22);
                AddColorButton(370, 10, 0xFFECF0F1);
                AddColorButton(400, 10, 0xFFD4AC0D);
                AddColorButton(430, 10, 0xFF333333);
                AddColorButton(460, 10, 0xFFFFFFFF);
                AddColorButton(490, 10, 0xFF000000);
                
                // Clear button
                ColorBtns.Add(new Button() {
                    X = 550,
                    Y = 10,
                    Width = 60,
                    Height = 20,
                    UIntParam = 0xFF555555,
                    Name = "Clear"
                });
                
                // Initialize tool buttons
                ToolBtns = new List<Button>();
                AddToolButton(10, 40, "Pencil", PaintTool.Pencil);
                AddToolButton(80, 40, "Brush", PaintTool.Brush);
                AddToolButton(150, 40, "Eraser", PaintTool.Eraser);
                AddToolButton(220, 40, "Line", PaintTool.Line);
                AddToolButton(290, 40, "Rect", PaintTool.Rectangle);
                AddToolButton(360, 40, "Circle", PaintTool.Circle);
                AddToolButton(430, 40, "Fill", PaintTool.FillBucket);
                
                // Initialize brush size buttons
                SizeBtns = new List<Button>();
                AddSizeButton(550, 40, "1px", 1);
                AddSizeButton(590, 40, "3px", 3);
                AddSizeButton(630, 40, "5px", 5);
                AddSizeButton(670, 40, "10px", 10);
                AddSizeButton(710, 40, "20px", 20);
                
                img = new Image(Width, Height);
                fixed (int* p = img.RawData)
                    g = new Graphics(Width, Height, (uint*)p);
                g.Clear(BackgroundColor);
                CurrentColor = 0xFFF0F0F0;
                LastX = -1;
                LastY = -1;
            } catch {
                _lastError = "Failed to initialize Paint";
                Title = "Paint - Initialization Error";
            }
        }
        
        public override void OnDraw() {
            try {
                base.OnDraw();
                
                // Check if we have an error
                if (_lastError != null) {
                    if (WindowManager.font != null) {
                        WindowManager.font.DrawString(X + 10, Y + 10, "Error: " + _lastError);
                    }
                    return;
                }
                
                // Validate state before drawing
                if (img == null || g == null || ColorBtns == null || ToolBtns == null || SizeBtns == null) {
                    if (WindowManager.font != null) {
                        WindowManager.font.DrawString(X + 10, Y + 10, "Paint not properly initialized");
                    }
                    return;
                }
                
                // Draw canvas
                Framebuffer.Graphics.DrawImage(X, Y + ToolbarHeight, img, false);
                
                // Draw color palette
                for (int i = 0; i < ColorBtns.Count; i++) {
                    try {
                        var btn = ColorBtns[i];
                        if (btn == null) continue;
                        
                        Framebuffer.Graphics.FillRectangle(X + btn.X, Y + btn.Y, btn.Width, btn.Height, btn.UIntParam);
                        
                        // Highlight selected color
                        if (btn.UIntParam == CurrentColor && btn.Name != "Clear") {
                            Framebuffer.Graphics.DrawRectangle(X + btn.X - 2, Y + btn.Y - 2, btn.Width + 4, btn.Height + 4, 0xFFFFFF00, 2);
                        }
                        
                        if (btn.Name == "Clear" && WindowManager.font != null) {
                            WindowManager.font.DrawString(X + btn.X + 10, Y + btn.Y + 2, btn.Name);
                        }
                    } catch {
                        // Skip this button if error occurs
                    }
                }
                
                // Draw tool buttons
                for (int i = 0; i < ToolBtns.Count; i++) {
                    try {
                        var btn = ToolBtns[i];
                        if (btn == null) continue;
                        
                        bool isSelected = _toolMap != null && _toolMap.ContainsKey(btn) && _toolMap[btn] == _currentTool;
                        uint btnColor = isSelected ? 0xFF4A8FD8 : 0xFF3A3A3A;
                        Framebuffer.Graphics.FillRectangle(X + btn.X, Y + btn.Y, btn.Width, btn.Height, btnColor);
                        Framebuffer.Graphics.DrawRectangle(X + btn.X, Y + btn.Y, btn.Width, btn.Height, 0xFF5A5A5A, 1);
                        
                        if (WindowManager.font != null && btn.Name != null) {
                            int textX = X + btn.X + (btn.Width / 2) - (btn.Name.Length * WindowManager.font.FontSize / 4);
                            WindowManager.font.DrawString(textX, Y + btn.Y + 3, btn.Name);
                        }
                    } catch {
                        // Skip this button if error occurs
                    }
                }
                
                // Draw size buttons
                for (int i = 0; i < SizeBtns.Count; i++) {
                    try {
                        var btn = SizeBtns[i];
                        if (btn == null) continue;
                        
                        bool isSelected = _sizeMap != null && _sizeMap.ContainsKey(btn) && _sizeMap[btn] == _brushSize;
                        uint btnColor = isSelected ? 0xFF4A8FD8 : 0xFF3A3A3A;
                        Framebuffer.Graphics.FillRectangle(X + btn.X, Y + btn.Y, btn.Width, btn.Height, btnColor);
                        Framebuffer.Graphics.DrawRectangle(X + btn.X, Y + btn.Y, btn.Width, btn.Height, 0xFF5A5A5A, 1);
                        
                        if (WindowManager.font != null && btn.Name != null) {
                            int textX = X + btn.X + 5;
                            WindowManager.font.DrawString(textX, Y + btn.Y + 3, btn.Name);
                        }
                    } catch {
                        // Skip this button if error occurs
                    }
                }
                
                // Draw current tool and color info
                if (WindowManager.font != null) {
                    try {
                        string toolInfo = $"Tool: {_currentTool} | Size: {_brushSize}px";
                        WindowManager.font.DrawString(X + 10, Y + 65, toolInfo);
                    } catch {
                        // Ignore error drawing info
                    }
                }
            } catch {
                // Catch any drawing errors to prevent crashes
                _lastError = "Drawing error occurred";
            }
        }

        public override void OnInput() {
            try {
                base.OnInput();
                
                // Don't process input if we have an error
                if (_lastError != null || img == null || g == null) return;

                if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                    if (Control.MousePosition.X >= X && Control.MousePosition.X <= X + Width && 
                        Control.MousePosition.Y >= Y && Control.MousePosition.Y <= Y + Height) {
                        WindowManager.MouseHandled = true;
                        
                        int mouseX = Control.MousePosition.X - X;
                        int mouseY = Control.MousePosition.Y - Y;
                        
                        // Handle toolbar clicks
                        if (mouseY < ToolbarHeight) {
                            HandleToolbarClick(mouseX, mouseY);
                        } else {
                            // Drawing on canvas
                            int canvasX = mouseX;
                            int canvasY = mouseY - ToolbarHeight;
                            
                            // Bounds check
                            if (canvasX >= 0 && canvasX < Width && canvasY >= 0 && canvasY < Height - ToolbarHeight) {
                                if (!_isDrawing) {
                                    _startX = canvasX;
                                    _startY = canvasY;
                                    _isDrawing = true;
                                }
                                
                                HandleDrawing(canvasX, canvasY);
                            }
                        }
                    }
                } else {
                    if (_isDrawing) {
                        // Finish drawing for shape tools
                        int canvasX = Control.MousePosition.X - X;
                        int canvasY = Control.MousePosition.Y - Y - ToolbarHeight;
                        
                        // Bounds check before finishing
                        if (canvasX >= 0 && canvasX < Width && canvasY >= 0 && canvasY < Height - ToolbarHeight) {
                            FinishDrawing(canvasX, canvasY);
                        }
                        _isDrawing = false;
                    }
                    WindowManager.MouseHandled = false;
                }

                LastX = Control.MousePosition.X - X;
                LastY = Control.MousePosition.Y - Y - ToolbarHeight;
            } catch {
                // Catch input handling errors
                _lastError = "Input handling error";
                _isDrawing = false;
            }
        }
        
        private void HandleToolbarClick(int mouseX, int mouseY) {
            try {
                if (ColorBtns == null || ToolBtns == null || SizeBtns == null) return;
                
                // Check color buttons
                for (int i = 0; i < ColorBtns.Count; i++) {
                    try {
                        var btn = ColorBtns[i];
                        if (btn == null) continue;
                        
                        if (mouseX > btn.X && mouseX < btn.X + btn.Width && 
                            mouseY > btn.Y && mouseY < btn.Y + btn.Height) {
                            if (btn.Name == "Clear" && g != null) {
                                g.Clear(BackgroundColor);
                            } else {
                                CurrentColor = btn.UIntParam;
                            }
                            return;
                        }
                    } catch {
                        // Skip this button
                    }
                }
                
                // Check tool buttons
                for (int i = 0; i < ToolBtns.Count; i++) {
                    try {
                        var btn = ToolBtns[i];
                        if (btn == null) continue;
                        
                        if (mouseX > btn.X && mouseX < btn.X + btn.Width && 
                            mouseY > btn.Y && mouseY < btn.Y + btn.Height) {
                            if (_toolMap != null && _toolMap.ContainsKey(btn)) {
                                _currentTool = _toolMap[btn];
                            }
                            return;
                        }
                    } catch {
                        // Skip this button
                    }
                }
                
                // Check size buttons
                for (int i = 0; i < SizeBtns.Count; i++) {
                    try {
                        var btn = SizeBtns[i];
                        if (btn == null) continue;
                        
                        if (mouseX > btn.X && mouseX < btn.X + btn.Width && 
                            mouseY > btn.Y && mouseY < btn.Y + btn.Height) {
                            if (_sizeMap != null && _sizeMap.ContainsKey(btn)) {
                                _brushSize = _sizeMap[btn];
                            }
                            return;
                        }
                    } catch {
                        // Skip this button
                    }
                }
            } catch {
                // Catch toolbar click errors
            }
        }
        
        private void HandleDrawing(int canvasX, int canvasY) {
            try {
                // Additional bounds check
                if (canvasX < 0 || canvasY < 0 || canvasX >= Width || canvasY >= Height - ToolbarHeight) return;
                
                switch (_currentTool) {
                    case PaintTool.Pencil:
                        DrawPixel(canvasX, canvasY, CurrentColor);
                        break;
                        
                    case PaintTool.Brush:
                        DrawBrush(canvasX, canvasY, CurrentColor, _brushSize);
                        // Draw line from last position for smooth drawing
                        if (LastX >= 0 && LastY >= 0) {
                            DrawBrushLine(LastX, LastY, canvasX, canvasY, CurrentColor, _brushSize);
                        }
                        break;
                        
                    case PaintTool.Eraser:
                        DrawBrush(canvasX, canvasY, BackgroundColor, _brushSize);
                        if (LastX >= 0 && LastY >= 0) {
                            DrawBrushLine(LastX, LastY, canvasX, canvasY, BackgroundColor, _brushSize);
                        }
                        break;
                        
                    case PaintTool.Line:
                    case PaintTool.Rectangle:
                    case PaintTool.Circle:
                        // These tools draw on mouse up
                        break;
                        
                    case PaintTool.FillBucket:
                        if (!_isDrawing || (_startX == canvasX && _startY == canvasY)) {
                            FloodFill(canvasX, canvasY, CurrentColor);
                            _isDrawing = false;
                        }
                        break;
                }
            } catch {
                // Catch drawing errors
                _lastError = "Drawing operation failed";
            }
        }
        
        private void FinishDrawing(int endX, int endY) {
            try {
                // Bounds check
                if (endX < 0 || endY < 0 || endX >= Width || endY >= Height - ToolbarHeight) return;
                
                switch (_currentTool) {
                    case PaintTool.Line:
                        DrawThickLine(_startX, _startY, endX, endY, CurrentColor, _brushSize);
                        break;
                        
                    case PaintTool.Rectangle:
                        DrawThickRectangle(_startX, _startY, endX - _startX, endY - _startY, CurrentColor, _brushSize);
                        break;
                        
                    case PaintTool.Circle:
                        int radius = (int)System.Math.Sqrt((endX - _startX) * (endX - _startX) + (endY - _startY) * (endY - _startY));
                        DrawThickCircle(_startX, _startY, radius, CurrentColor, _brushSize);
                        break;
                }
            } catch {
                // Catch finish drawing errors
            }
        }
        
        // Drawing primitives
        
        private void DrawPixel(int x, int y, uint color) {
            try {
                if (g == null) return;
                if (x >= 0 && y >= 0 && x < Width && y < Height - ToolbarHeight) {
                    g.DrawPoint(x, y, color);
                }
            } catch {
                // Ignore pixel drawing errors
            }
        }
        
        private void DrawBrush(int centerX, int centerY, uint color, int size) {
            try {
                if (size <= 0) size = 1;
                if (size > 100) size = 100; // Limit brush size
                
                int radius = size / 2;
                for (int dy = -radius; dy <= radius; dy++) {
                    for (int dx = -radius; dx <= radius; dx++) {
                        // Circular brush
                        if (dx * dx + dy * dy <= radius * radius) {
                            DrawPixel(centerX + dx, centerY + dy, color);
                        }
                    }
                }
            } catch {
                // Ignore brush drawing errors
            }
        }
        
        private void DrawBrushLine(int x0, int y0, int x1, int y1, uint color, int size) {
            try {
                // Bresenham's line algorithm with brush
                int dx = x1 - x0;
                if (dx < 0) dx = -dx;
                int dy = y1 - y0;
                if (dy < 0) dy = -dy;
                
                int sx = x0 < x1 ? 1 : -1;
                int sy = y0 < y1 ? 1 : -1;
                int err = dx - dy;
                
                int x = x0;
                int y = y0;
                
                int maxIterations = 10000; // Prevent infinite loops
                int iterations = 0;
                
                while (iterations < maxIterations) {
                    DrawBrush(x, y, color, size);
                    
                    if (x == x1 && y == y1) break;
                    
                    int e2 = 2 * err;
                    if (e2 > -dy) {
                        err -= dy;
                        x += sx;
                    }
                    if (e2 < dx) {
                        err += dx;
                        y += sy;
                    }
                    
                    iterations++;
                }
            } catch {
                // Ignore line drawing errors
            }
        }
        
        private void DrawThickLine(int x0, int y0, int x1, int y1, uint color, int thickness) {
            try {
                DrawBrushLine(x0, y0, x1, y1, color, thickness);
            } catch {
                // Ignore thick line errors
            }
        }
        
        private void DrawThickRectangle(int x, int y, int width, int height, uint color, int thickness) {
            try {
                // Normalize rectangle
                if (width < 0) {
                    x += width;
                    width = -width;
                }
                if (height < 0) {
                    y += height;
                    height = -height;
                }
                
                // Sanity check
                if (width > 10000 || height > 10000) return;
                
                // Draw four sides
                DrawThickLine(x, y, x + width, y, color, thickness);
                DrawThickLine(x + width, y, x + width, y + height, color, thickness);
                DrawThickLine(x + width, y + height, x, y + height, color, thickness);
                DrawThickLine(x, y + height, x, y, color, thickness);
            } catch {
                // Ignore rectangle drawing errors
            }
        }
        
        private void DrawThickCircle(int centerX, int centerY, int radius, uint color, int thickness) {
            try {
                if (radius < 0) radius = -radius;
                if (radius > 5000) radius = 5000; // Limit circle size
                if (thickness <= 0) thickness = 1;
                if (thickness > 50) thickness = 50;
                
                // Midpoint circle algorithm
                int x = 0;
                int y = radius;
                int d = 3 - 2 * radius;
                
                int maxIterations = radius * 2;
                int iterations = 0;
                
                while (y >= x && iterations < maxIterations) {
                    // Draw 8 octants with thickness
                    for (int t = 0; t < thickness; t++) {
                        int r = radius - thickness / 2 + t;
                        if (r < 0) continue;
                        
                        try {
                            int offsetX = (int)(x * r / (float)radius);
                            int offsetY = (int)(y * r / (float)radius);
                            
                            DrawPixel(centerX + offsetX, centerY + offsetY, color);
                            DrawPixel(centerX - offsetX, centerY + offsetY, color);
                            DrawPixel(centerX + offsetX, centerY - offsetY, color);
                            DrawPixel(centerX - offsetX, centerY - offsetY, color);
                            DrawPixel(centerX + offsetY, centerY + offsetX, color);
                            DrawPixel(centerX - offsetY, centerY + offsetX, color);
                            DrawPixel(centerX + offsetY, centerY - offsetX, color);
                            DrawPixel(centerX - offsetY, centerY - offsetX, color);
                        } catch {
                            // Skip this octant point
                        }
                    }
                    
                    x++;
                    if (d > 0) {
                        y--;
                        d = d + 4 * (x - y) + 10;
                    } else {
                        d = d + 4 * x + 6;
                    }
                    
                    iterations++;
                }
            } catch {
                // Ignore circle drawing errors
            }
        }
        
        private void FloodFill(int x, int y, uint newColor) {
            try {
                if (g == null) return;
                if (x < 0 || y < 0 || x >= Width || y >= Height - ToolbarHeight) return;
                
                uint targetColor = g.GetPoint(x, y);
                if (targetColor == newColor) return;
                
                // Simple flood fill using stack-based approach
                List<Point> stack = new List<Point>();
                stack.Add(new Point(x, y));
                
                int processed = 0;
                int maxPixels = 50000; // Limit to prevent hanging
                
                while (stack.Count > 0 && processed < maxPixels) {
                    if (stack.Count == 0) break;
                    
                    Point p = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    
                    if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height - ToolbarHeight) continue;
                    
                    uint currentColor = g.GetPoint(p.X, p.Y);
                    if (currentColor != targetColor) continue;
                    
                    g.DrawPoint(p.X, p.Y, newColor);
                    processed++;
                    
                    // Add neighbors with bounds checking
                    if (p.X + 1 < Width) stack.Add(new Point(p.X + 1, p.Y));
                    if (p.X - 1 >= 0) stack.Add(new Point(p.X - 1, p.Y));
                    if (p.Y + 1 < Height - ToolbarHeight) stack.Add(new Point(p.X, p.Y + 1));
                    if (p.Y - 1 >= 0) stack.Add(new Point(p.X, p.Y - 1));
                }
                
                if (stack != null) stack.Dispose();
            } catch {
                // Ignore flood fill errors
            }
        }
        
        // Button helpers
        
        private void AddColorButton(int x, int y, uint color) {
            try {
                if (ColorBtns != null) {
                    ColorBtns.Add(new Button() {
                        X = x,
                        Y = y,
                        Width = 20,
                        Height = 20,
                        UIntParam = color,
                        Name = ""
                    });
                }
            } catch {
                // Ignore button creation error
            }
        }
        
        private void AddToolButton(int x, int y, string name, PaintTool tool) {
            try {
                if (ToolBtns != null && _toolMap != null) {
                    var btn = new Button() {
                        X = x,
                        Y = y,
                        Width = 60,
                        Height = 20,
                        Name = name
                    };
                    ToolBtns.Add(btn);
                    _toolMap.Add(btn, tool);
                }
            } catch {
                // Ignore button creation error
            }
        }
        
        private void AddSizeButton(int x, int y, string name, int size) {
            try {
                if (SizeBtns != null && _sizeMap != null) {
                    var btn = new Button() {
                        X = x,
                        Y = y,
                        Width = 35,
                        Height = 20,
                        Name = name
                    };
                    SizeBtns.Add(btn);
                    _sizeMap.Add(btn, size);
                }
            } catch {
                // Ignore button creation error
            }
        }
    }
}
