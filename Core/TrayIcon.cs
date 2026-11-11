using System;
using System.Drawing;
using System.Windows.Forms;

namespace PincabVolume;

/// <summary>Notification-area icon with a dark context menu.</summary>
public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _notifyIcon;
    readonly Icon _icon;

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

        _icon = LoadIcon();
        _notifyIcon = new NotifyIcon { Icon = _icon, Text = "Pincab Volume", ContextMenuStrip = menu, Visible = true };
        _notifyIcon.DoubleClick += (_, _) => openSettings();
    }

    /// <summary>Picks the best-fitting size (DPI-aware) from the embedded multi-size .ico.</summary>
    static Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"));
        using var stream = resource.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
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
