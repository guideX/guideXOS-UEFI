using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using System.Collections.Generic;
using System.Windows.Forms;

namespace guideXOS.DefaultApps
{
    /// <summary>
    /// A window that lists connected USB drives.
    /// </summary>
    internal class USBDrives : Window
    {
        private List<DriveInfo> _drives;
        private int _scroll;

        public USBDrives(int x, int y, int w, int h) : base(x, y, w, h)
        {
            Title = "USB Drives";
            ShowInTaskbar = true;
            IsResizable = true;
            _drives = new List<DriveInfo>();
            RefreshDrives();
        }

        private void RefreshDrives()
        {
            _drives.Clear();
            var usbDevices = USBStorage.GetAll();
            for (int i = 0; i < usbDevices.Length; i++)
            {
                var dev = usbDevices[i];
                var disk = USBMSC.TryOpenDisk(dev);
                if (disk != null && disk.IsReady)
                {
                    _drives.Add(new DriveInfo
                    {
                        Name = "USB Drive " + (i + 1),
                        Type = DriveInfo.DriveType.USB,
                        TotalSize = disk.TotalBlocks * disk.LogicalBlockSize,
                        IsReady = true,
                        Tag = dev,
                        FileSystem = new AutoFS(disk)
                    });
                }
            }
        }

        public override void OnDraw()
        {
            base.OnDraw();
            int y = Y + 8;
            for (int i = 0; i < _drives.Count; i++)
            {
                var drive = _drives[i];
                int x = X + 8;
                Framebuffer.Graphics.DrawImage(x, y, Icons.FolderIcon(32));
                WindowManager.font.DrawString(x + 40, y + 8, drive.Name);
                y += 40;
            }
        }

        public override void OnInput()
        {
            base.OnInput();
            if (Control.MouseButtons == MouseButtons.Left)
            {
                int my = Control.MousePosition.Y;
                int y = Y + 8;
                for (int i = 0; i < _drives.Count; i++)
                {
                    if (my >= y && my < y + 40)
                    {
                        var drive = _drives[i];
                        Desktop.LaunchComputerFilesDrive(drive.Name,
                            X + 20, Y + 20);
                        break;
                    }
                    y += 40;
                }
            }
        }
    }
}
