using guideXOS.Graph;
using guideXOS.GUI;
using guideXOS.GUI.Widgets;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Calculator
    /// </summary>
    internal class Calculator : Window {
        private ApplicationServiceContext _serviceContext;
        private ApplicationServiceAccess _services;
        /// <summary>
        /// Image
        /// </summary>
        private Image image;
        /// <summary>
        /// Graphics
        /// </summary>
        private Graphics g;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public unsafe Calculator(int X, int Y) : base(X, Y, 450, 140) {
            _serviceContext = null;
            _services = null;
            InitializeCalculator();
        }

        public unsafe Calculator(int X, int Y,
                ApplicationServiceContext serviceContext,
                ApplicationServiceAccess services) : base(X, Y, 450, 140) {
            _serviceContext = serviceContext;
            _services = services;
            InitializeCalculator();
        }

        private unsafe void InitializeCalculator() {
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            Title = "Calculator";
            Btns = new List<Button>();
            image = new Image(Width, Height);
            fixed (int* p = image.RawData) g = new Graphics(image.Width, image.Height, (uint*)p);
            PressedButton = new();
            g.FillRectangle(0, 0, Width, Height, 0xFF222222);
            g.FillRectangle(0, 0, Width, 20, 0xFF333333);

            //7
            AddButton(0, 30, "7");
            //8
            AddButton(70, 30, "8");
            //9
            AddButton(140, 30, "9");

            //4
            AddButton(0, 60, "4");
            //5
            AddButton(70, 60, "5");
            //6
            AddButton(140, 60, "6");

            //1
            AddButton(0, 90, "1");
            //2
            AddButton(70, 90, "2");
            //3
            AddButton(140, 90, "3");

            //0
            AddButton(70, 120, "0");

            //C
            AddButton(210, 30, "C");
            //+
            AddButton(210, 60, "+");
            //-
            AddButton(210, 90, "-");
            //=
            AddButton(210, 120, "=");

            //*
            AddButton(270, 30, "*");
            //slash
            AddButton(270, 60, "/");
            //<
            AddButton(270, 90, "<");
            //>
            AddButton(270, 120, ">");
        }
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            base.OnDraw();
            Framebuffer.Graphics.DrawImage(X, Y, image);
            string v = ValueToDisplay.ToString();
            WindowManager.font.DrawString(X, Y + 2, v);
            if (Pressed) {
                // Pressed visual already handled by Draw overlay; no extra fill needed
                Framebuffer.Graphics.FillRectangle(X + PressedButton.X, Y + PressedButton.Y, 60, 20, 0xFF222222);
                int i = WindowManager.font.MeasureString(PressedButton.Name);
                WindowManager.font.DrawString(X + PressedButton.X + 60 / 2 - i / 2, Y + PressedButton.Y + 2, PressedButton.Name);
            }
            v.Dispose();
        }
        /// <summary>
        /// Pressed
        /// </summary>
        bool Pressed = false;
        /// <summary>
        /// Pressed Button
        /// </summary>
        Button PressedButton;
        /// <summary>
        /// On Input
        /// </summary>
        public override void OnInput() {
            base.OnInput();
            if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                if (!Pressed) {
                    for (int i = 0; i < Btns.Count; i++) {
                        if (Control.MousePosition.X > X + Btns[i].X && Control.MousePosition.X < X + Btns[i].X + Btns[i].Width && Control.MousePosition.Y > Y + Btns[i].Y && Control.MousePosition.Y < Y + Btns[i].Y + Btns[i].Height) {
                            ProcessButton(Btns[i]);
                            PressedButton = Btns[i];
                            Pressed = true;
                        }
                    }
                }
            } else {
                Pressed = false;
            }
        }
        /// <summary>
        /// Operation
        /// </summary>
        enum Opreation {
            None,
            Plus,
            Minus,
            Star
        }
        /// <summary>
        /// Num 1
        /// </summary>
        ulong Num1 = 0;
        /// <summary>
        /// Num 2
        /// </summary>
        ulong Num2 = 0;
        /// <summary>
        /// Value to Display
        /// </summary>
        ulong ValueToDisplay = 0;
        /// <summary>
        /// Operation
        /// </summary>
        Opreation opreation = Opreation.None;
        /// <summary>
        /// Process Button
        /// </summary>
        /// <param name="btn"></param>
        private unsafe void ProcessButton(Button btn) {
            if (btn.Name[0] >= 0x30 && btn.Name[0] <= 0x39) {
                if (Num2 == 0) {
                    Num2 = btn.Name[0] - 0x30UL;
                } else {
                    Num2 *= 10;
                    Num2 += btn.Name[0] - 0x30UL;
                }
                ValueToDisplay = Num2;
            } else if (btn.Name == "*") {
                if (Num1 == 0) Num1 = Num2;
                opreation = Opreation.Star;
                Num2 = 0;
            } else if (btn.Name == "+") {
                if (Num1 == 0) Num1 = Num2;
                opreation = Opreation.Plus;
                Num2 = 0;
            } else if (btn.Name == "-") {
                if (Num1 == 0) Num1 = Num2;
                opreation = Opreation.Minus;
                Num2 = 0;
            } else if (btn.Name == "C") {
                Num1 = 0;
                Num2 = 0;
                ValueToDisplay = 0;
            } else if (btn.Name == "=") {
                if (opreation == Opreation.Plus) {
                    NotifyApplication("Add " + Num1 + " to " + Num2, false);
                    Num1 += Num2;
                } else if (opreation == Opreation.Minus) {
                    NotifyApplication("Subtract " + Num1 + " from " + Num2, false);
                    if (Num1 >= Num2)
                        Num1 -= Num2;
                    else {
                        Num1 = 0;
                        Num2 = 0;
                    }
                } else if (opreation == Opreation.Star) {
                    //Num1 = Num1 * Num2;
                    Num1 *= Num2;
                    NotifyApplication("Multiply " + Num1 + " by " + Num2, false);
                } else if (opreation == Opreation.None) {
                    Num2 = 0;
                }
                ValueToDisplay = Num1;
            }
        }

        private void NotifyApplication(string message, bool error) {
            if (_serviceContext == null || _services == null ||
                    _services.Notifications == null) return;
            _services.Notifications.Publish(_serviceContext,
                ApplicationNotificationRequest.Create(
                    "Calculator", message,
                    error ? ApplicationNotificationSeverity.Error :
                        ApplicationNotificationSeverity.Info));
        }
        List<Button> Btns;
        /// <summary>
        /// Add Button
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="s"></param>
        private void AddButton(int X, int Y, string s) {
            g.FillRectangle(X, Y, 60, 20, 0xFF333333);
            int i = WindowManager.font.MeasureString(s);
            WindowManager.font.DrawString(X + 60 / 2 - i / 2, Y + 2, s, g);
            Btns.Add(new Button() {
                X = X,
                Y = Y,
                Width = 60,
                Height = 20,
                Name = s
            });
        }
    }
}
