using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace PincabVolume.Views;

/// <summary>Click-through, non-activating topmost volume overlay (fine over VPX in windowed fullscreen).</summary>
public partial class OsdWindow : Window
{
    static readonly string[] VolumeGlyphs = { "\uE74F", "\uE993", "\uE994", "\uE995" }; // mute, low, mid, high

    readonly DispatcherTimer _hideTimer = new();

    public OsdWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE)
                | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT);
        };
        _hideTimer.Tick += (_, _) => FadeOut();
    }

    public void ShowVolume(string profile, int percent, string table, bool preview = false)
    {
        var s = App.Config.Osd;
        if (!s.Enabled && !preview) return;

        ProfileText.Text = profile;
        TableText.Text = table ?? "System";
        PctText.Text = percent + "%";
        IconText.Text = VolumeGlyphs[percent == 0 ? 0 : percent < 34 ? 1 : percent < 67 ? 2 : 3];
        FillCol.Width = new GridLength(Math.Max(percent, 0.001), GridUnitType.Star);
        RestCol.Width = new GridLength(Math.Max(100 - percent, 0.001), GridUnitType.Star);
        ScaleT.ScaleX = ScaleT.ScaleY = s.Scale;
        RotT.Angle = s.Rotation;

        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }

        UpdateLayout();
        // Twice: moving to a monitor with another DPI resizes the window, which changes the anchored position.
        Reposition(s);
        Reposition(s);

        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.5, s.DurationSec));
        _hideTimer.Start();
    }

    void FadeOut()
    {
        _hideTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) =>
        {
            if (!_hideTimer.IsEnabled) Hide(); // not re-shown during the fade
        };
        BeginAnimation(OpacityProperty, fade);
    }

    void Reposition(OsdSettings s)
    {
        var screens = Screen.AllScreens;
        var bounds = (s.MonitorIndex >= 0 && s.MonitorIndex < screens.Length
            ? screens[s.MonitorIndex]
            : Screen.PrimaryScreen).Bounds;

        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        int h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        int col = (int)s.Anchor % 3, row = (int)s.Anchor / 3;

        int x = col switch
        {
            0 => bounds.Left + s.OffsetX,
            1 => bounds.Left + (bounds.Width - w) / 2 + s.OffsetX,
            _ => bounds.Right - w - s.OffsetX
        };
        int y = row switch
        {
            0 => bounds.Top + s.OffsetY,
            1 => bounds.Top + (bounds.Height - h) / 2 + s.OffsetY,
            _ => bounds.Bottom - h - s.OffsetY
        };

        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, x, y, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }
}
