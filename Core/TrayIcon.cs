using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PincabVolume;

/// <summary>Notification-area icon with a dark context menu.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _notifyIcon;
    readonly Icon _icon;

    /// <summary>Generated app icon, also used for the settings window.</summary>
    public Icon Icon => _icon;

    public TrayIcon(Action openSettings, Action exit)
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            BackColor = DarkMenuColors.Background,
            ForeColor = Color.FromArgb(231, 234, 242),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        menu.Items.Add("Settings", null, (_, _) => openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());
        foreach (ToolStripItem item in menu.Items) item.Padding = new Padding(8, 4, 24, 4);

        _icon = CreateIcon();
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = "Pincab Volume", ContextMenuStrip = menu, Visible = true };
        _notifyIcon.DoubleClick += (_, _) => openSettings();
    }

    static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        using (var fill = new LinearGradientBrush(new Rectangle(0, 0, 32, 32),
                   Color.FromArgb(57, 208, 255), Color.FromArgb(182, 92, 255), 45f))
        using (var font = new Font("Segoe UI Symbol", 16, FontStyle.Bold))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(fill, 1, 1, 30, 30);
            g.DrawString("♪", font, Brushes.White, 6, 3);
        }

        var handle = bmp.GetHicon();
        using var temp = Icon.FromHandle(handle);
        var icon = (Icon)temp.Clone();
        Native.DestroyIcon(handle);
        return icon;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public static readonly Color Background = Color.FromArgb(22, 25, 34);
        static readonly Color Hover = Color.FromArgb(39, 44, 58);
        static readonly Color Border = Color.FromArgb(42, 47, 61);

        public override Color ToolStripDropDownBackground => Background;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Background;
    }
}
