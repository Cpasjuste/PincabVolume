using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PincabVolume.Views;

public record TableEntry(string Name, string Summary);

public partial class MainWindow : Window
{
    public MainWindow()
    {
        DataContext = App.Config;
        RefreshDevices();
        RefreshMonitors();
        InitializeComponent();
        ProfileList.SelectedIndex = App.Config.Profiles.Count > 0 ? 0 : -1;

        SourceInitialized += (_, _) => ApplyDarkChrome();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) return;
            RefreshDevices();
            RefreshMonitors();
            RefreshTables();
            UpdateStatus();
        };
        App.Volume.ContextChanged += UpdateStatus;
        App.Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppConfig.ActiveProfileId)) UpdateStatus();
        };
        // Live preview while tweaking OSD settings.
        App.Config.Osd.PropertyChanged += (_, _) =>
        {
            if (IsVisible) PreviewOsd();
        };
        UpdateStatus();
    }

    public ObservableCollection<AudioDeviceInfo> Devices { get; } = new();
    public ObservableCollection<string> Monitors { get; } = new();
    public ObservableCollection<TableEntry> Tables { get; } = new();
    public OsdAnchor[] Anchors { get; } = Enum.GetValues<OsdAnchor>();
    public int[] Rotations { get; } = { 0, 90, 180, 270 };
    public string ConfigPath => ConfigService.FilePath;

    Profile Selected => ProfileList.SelectedItem as Profile;

    /// <summary>Dark Win11 title bar matching the window background.</summary>
    void ApplyDarkChrome()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var bg = ((SolidColorBrush)FindResource("BgBrush")).Color;
        int dark = 1, caption = bg.R | bg.G << 8 | bg.B << 16;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_BORDER_COLOR, ref caption, sizeof(int));
    }

    void UpdateStatus()
    {
        var table = App.Volume.CurrentTable;
        StatusDot.Fill = (Brush)FindResource(table != null ? "SuccessBrush" : "TextDimBrush");
        ContextText.Text = table ?? "System — no table running";
        ActiveText.Text = "Active profile: " + (App.Volume.ActiveProfile?.Name ?? "none");
    }

    /// <summary>Syncs the device list in place so the ComboBox selections are not reset.</summary>
    void RefreshDevices()
    {
        var fresh = AudioService.GetDevices();
        foreach (var p in App.Config.Profiles)
            if (!string.IsNullOrEmpty(p.DeviceId) && fresh.All(d => d.Id != p.DeviceId))
                fresh.Add(new AudioDeviceInfo(p.DeviceId, "(unavailable) " + p.DeviceId));

        for (int i = Devices.Count - 1; i >= 0; i--)
            if (fresh.All(f => f.Id != Devices[i].Id))
                Devices.RemoveAt(i);

        foreach (var f in fresh)
        {
            var existing = Devices.FirstOrDefault(d => d.Id == f.Id);
            if (existing == null) Devices.Add(f);
            else if (existing.Name != f.Name) Devices[Devices.IndexOf(existing)] = f;
        }
    }

    void RefreshMonitors()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var labels = screens.Select((s, i) =>
            $"{i + 1}: {s.Bounds.Width}×{s.Bounds.Height} at {s.Bounds.X},{s.Bounds.Y}{(s.Primary ? "  (primary)" : "")}");
        if (labels.SequenceEqual(Monitors)) return;

        var index = App.Config.Osd.MonitorIndex;
        Monitors.Clear();
        foreach (var l in labels) Monitors.Add(l);
        App.Config.Osd.MonitorIndex = index; // Clear() reset the bound SelectedIndex
    }

    void RefreshTables()
    {
        Tables.Clear();
        foreach (var (name, volumes) in App.Config.TableVolumes
                     .Where(t => t.Key != VolumeManager.SystemKey)
                     .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
        {
            var summary = string.Join("   ", App.Config.Profiles
                .Where(p => volumes.ContainsKey(p.Id))
                .Select(p => $"{p.Name} {volumes[p.Id]}%"));
            Tables.Add(new TableEntry(name, summary));
        }
    }

    void PreviewOsd() => App.Osd.ShowVolume(Selected?.Name ?? "Preview", 65, "Sample Table", preview: true);

    void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = new Profile { Name = $"Profile {App.Config.Profiles.Count + 1}" };
        App.Config.Profiles.Add(p);
        App.Config.ActiveProfileId ??= p.Id;
        ProfileList.SelectedItem = p;
    }

    void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        int index = ProfileList.SelectedIndex;
        App.Config.Profiles.Remove(p);
        if (App.Config.ActiveProfileId == p.Id) App.Config.ActiveProfileId = App.Config.Profiles.FirstOrDefault()?.Id;
        ProfileList.SelectedIndex = Math.Min(index, App.Config.Profiles.Count - 1);
    }

    void RefreshDevices_Click(object sender, RoutedEventArgs e) => RefreshDevices();

    void TestUp_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) App.Volume.Change(p, p.Step);
    }

    void TestDown_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) App.Volume.Change(p, -p.Step);
    }

    void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) App.Config.ActiveProfileId = p.Id;
    }

    void Preview_Click(object sender, RoutedEventArgs e) => PreviewOsd();

    void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(ConfigService.Dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ConfigService.Dir}\"") { UseShellExecute = true });
    }

    void Forget_Click(object sender, RoutedEventArgs e)
    {
        if (TableList.SelectedItem is not TableEntry t) return;
        App.Config.TableVolumes.Remove(t.Name);
        App.Store.SaveSoon();
        RefreshTables();
    }

    void Save_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Closing only hides to the tray; the app exits from the tray menu.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsExiting)
        {
            e.Cancel = true;
            App.Input.CancelCapture();
            App.Config.Configured = true;
            StartupService.Set(App.Config.RunAtLogon);
            App.Volume.ApplyContext();
            App.Store.SaveNow();
            Hide();
        }

        base.OnClosing(e);
    }
}
