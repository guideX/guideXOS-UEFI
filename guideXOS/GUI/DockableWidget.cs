using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using System.Windows.Forms;
using System.Drawing;

namespace guideXOS.GUI {
    /// <summary>
    /// Base class for widgets that can dock together
    /// </summary>
    internal abstract class DockableWidget : Window {
        protected const int Padding = 8;
        protected const int CloseBtnSize = 16;
        protected bool _closeHover = false;
        protected bool _dragging = false;
        protected int _dragOffsetX, _dragOffsetY;
        
        /// <summary>
        /// The container this widget is docked to (null if standalone)
        /// </summary>
        public WidgetContainer DockedContainer { get; set; }
        
        /// <summary>
        /// Docking distance threshold
        /// </summary>
        protected const int DockThreshold = 30;
        
        /// <summary>
        /// Preferred height for this widget when docked
        /// </summary>
        public abstract int PreferredHeight { get; }
        
        protected DockableWidget(int x, int y, int w, int h) : base(x, y, w, h) {
            BarHeight = 0; // No title bar
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
        }
        
        protected void HandleDragging() {
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool leftPressed = MouseEventDispatcher.WasPressedThisFrame(MouseButtons.Left);
            bool leftActive = leftDown || leftPressed;
            bool rightClick = Control.MouseButtons.HasFlag(MouseButtons.Right);
            
            // Handle right-click for context menu (only when standalone)
            if (rightClick && !_dragging && DockedContainer == null) {
                if (mx >= X && mx <= X + Width && my >= Y && my <= Y + Height) {
                    // Show context menu for standalone widget
                    if (Program.widgetContextMenu != null) {
                        Program.MarkUefiWidgetInput("RIGHT");
                        Program.widgetContextMenu.ShowForStandaloneWidget(this, mx, my);
                    }
                    return;
                }
            }
            
            if (leftActive) {
                // Start dragging if clicked anywhere in the widget (except close button)
                if (!_dragging && !_closeHover && mx >= X && mx <= X + Width && my >= Y && my <= Y + Height) {
                    _dragging = true;
                    Program.MarkUefiWidgetInput("DRAG_START");
                    _dragOffsetX = mx - X;
                    _dragOffsetY = my - Y;
                    
                    // Undock if we're in a container
                    if (DockedContainer != null) {
                        UndockFromContainer();
                    }
                }
                
                // Handle dragging
                if (_dragging) {
                    X = mx - _dragOffsetX;
                    Y = my - _dragOffsetY;
                    
                    // Clamp to screen bounds
                    if (X < 0) X = 0;
                    if (Y < 0) Y = 0;
                    if (X + Width > Framebuffer.Width) X = Framebuffer.Width - Width;
                    if (Y + Height > Framebuffer.Height) Y = Framebuffer.Height - Height;
                }
            } else {
                if (_dragging) {
                    // Check for docking when releasing
                    CheckForDocking();
                    Program.MarkUefiWidgetInput("DRAG_END");
                }
                _dragging = false;
            }
        }
        
        /// <summary>
        /// Try to dock to a nearby widget or container (used by context menu)
        /// </summary>
        public void TryDockToNearby() {
            CheckForDocking();
        }
        
        private void CheckForDocking() {
            // Look for other dockable widgets to dock with
            for (int i = 0; i < WindowManager.Windows.Count; i++) {
                var window = WindowManager.Windows[i];
                
                // Check if it's a container
                if (window is WidgetContainer container && window != this) {
                    if (IsNearWidget(container)) {
                        DockToContainer(container);
                        return;
                    }
                }
                
                // Check if it's another standalone dockable widget
                if (window is DockableWidget other && window != this && other.DockedContainer == null) {
                    if (IsNearWidget(other)) {
                        // Create a new container for both widgets
                        CreateContainerWith(other);
                        return;
                    }
                }
            }
        }
        
        private bool IsNearWidget(Window other) {
            // Check if this widget is close enough to another widget to dock
            int dx = System.Math.Abs(X - other.X);
            int dy = System.Math.Abs((Y + Height) - other.Y); // Bottom of this widget to top of other
            
            // Check if horizontally aligned and vertically close
            if (dx < DockThreshold && dy < DockThreshold) {
                return true;
            }
            
            // Also check if other is below this one
            dy = System.Math.Abs((other.Y + other.Height) - Y); // Bottom of other to top of this
            if (dx < DockThreshold && dy < DockThreshold) {
                return true;
            }
            
            return false;
        }
        
        private void CreateContainerWith(DockableWidget other) {
            // Create a new container
            var container = new WidgetContainer(System.Math.Min(X, other.X), System.Math.Min(Y, other.Y));
            WindowManager.MoveToEnd(container);
            container.Visible = true;
            
            // Add both widgets to the container
            container.AddWidget(this);
            container.AddWidget(other);
            
            // Hide the individual widgets
            Visible = false;
            other.Visible = false;
        }
        
        private void DockToContainer(WidgetContainer container) {
            container.AddWidget(this);
            Visible = false;
        }
        
        private void UndockFromContainer() {
            if (DockedContainer != null) {
                DockedContainer.RemoveWidget(this);
                DockedContainer = null;
                Visible = true;
                
                // Position at current mouse position
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                X = mx - _dragOffsetX;
                Y = my - _dragOffsetY;
            }
        }
        
        protected void DrawCloseButton(int x, int y) {
            // Button background
            uint btnColor = _closeHover ? 0xFFFF5555 : 0xFF883333;
            UIPrimitives.AFillRoundedRect(x, y, CloseBtnSize, CloseBtnSize, btnColor, 3);
            
            // X symbol
            uint xColor = _closeHover ? 0xFFFFFFFF : 0xFFCCCCCC;
            int pad = 4;
            
            // Draw X with 2 lines
            Framebuffer.Graphics.DrawLine(x + pad, y + pad, 
                                         x + CloseBtnSize - pad, y + CloseBtnSize - pad, xColor);
            Framebuffer.Graphics.DrawLine(x + CloseBtnSize - pad, y + pad, 
                                         x + pad, y + CloseBtnSize - pad, xColor);
            
            // Draw again shifted by 1px for thickness
            Framebuffer.Graphics.DrawLine(x + pad + 1, y + pad, 
                                         x + CloseBtnSize - pad + 1, y + CloseBtnSize - pad, xColor);
            Framebuffer.Graphics.DrawLine(x + CloseBtnSize - pad, y + pad + 1, 
                                         x + pad, y + CloseBtnSize - pad + 1, xColor);
        }
        
        /// <summary>
        /// Draw the widget content (should be implemented by derived classes)
        /// </summary>
        /// <param name="contentX">X position for content</param>
        /// <param name="contentY">Y position for content</param>
        /// <param name="contentWidth">Available width for content</param>
        public abstract void DrawContent(int contentX, int contentY, int contentWidth);
    }
}
