using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PincabVolume;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum OsdAnchor
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight
}

/// <summary>One audio output device with its own key bindings.</summary>
public class Profile : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set => Set(ref field, value); } = "New profile";

    /// <summary>Endpoint id; empty means "default output device".</summary>
    public string DeviceId { get => field ?? ""; set => Set(ref field, value); } = "";

    /// <summary>Last known friendly name; used to find the device again if Windows gives it a new id.</summary>
    public string DeviceName { get; set => Set(ref field, value); }

    /// <summary>Optional key/button that must be held for VolUp/VolDown to act on this profile.</summary>
    [JsonPropertyName("Hotkey")] // name kept for config compatibility
    public string HoldKey { get; set => Set(ref field, value); }

    public string VolUp { get; set => Set(ref field, value); }
    public string VolDown { get; set => Set(ref field, value); }
    public int Step { get; set => Set(ref field, value); } = 2;
    public int MaxVolume { get; set => Set(ref field, value); } = 100;

    /// <summary>When on, tables without a saved volume start at <see cref="DefaultVolume"/> instead of the System volume.</summary>
    public bool UseDefaultVolume { get; set => Set(ref field, value); }

    public int DefaultVolume { get; set => Set(ref field, value); } = 50;
}

public class OsdSettings : Observable
{
    public bool Enabled { get; set => Set(ref field, value); } = true;
    public int MonitorIndex { get; set => Set(ref field, value); }
    public OsdAnchor Anchor { get; set => Set(ref field, value); } = OsdAnchor.BottomCenter;
    public int OffsetX { get; set => Set(ref field, value); } = 40;
    public int OffsetY { get; set => Set(ref field, value); } = 40;
    public int Rotation { get; set => Set(ref field, value); }
    public double Scale { get; set => Set(ref field, value); } = 1;
    public double DurationSec { get; set => Set(ref field, value); } = 1.8;
}

public class AppConfig : Observable
{
    public bool Configured { get; set; }
    public bool StartMinimized { get; set => Set(ref field, value); } = true;
    public bool RunAtLogon { get; set => Set(ref field, value); }
    public string ActiveProfileId { get; set => Set(ref field, value); }
    public ObservableCollection<Profile> Profiles { get; set; } = new();
    public OsdSettings Osd { get; set; } = new();

    /// <summary>Table name (or <see cref="VolumeManager.SystemKey"/>) → profile id → volume %.</summary>
    public Dictionary<string, Dictionary<string, int>> TableVolumes { get; set; } = new();
}

public record AudioDeviceInfo(string Id, string Name);
