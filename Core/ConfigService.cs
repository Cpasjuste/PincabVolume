using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;

namespace PincabVolume;

/// <summary>Loads/saves <see cref="AppConfig"/> as JSON in %AppData%\PincabVolume.</summary>
public sealed class ConfigService
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PincabVolume");

    public static readonly string FilePath = Path.Combine(Dir, "config.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public AppConfig Config { get; private set; } = new();

    public ConfigService() => _debounce.Tick += (_, _) => SaveNow();

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new();
        }
        catch
        {
            // Keep the broken file around for inspection and start fresh.
            try { File.Move(FilePath, FilePath + ".bad", true); } catch { }
            Config = new();
        }

        if (Config.Profiles.All(p => p.Id != Config.ActiveProfileId))
            Config.ActiveProfileId = Config.Profiles.FirstOrDefault()?.Id;
    }

    /// <summary>Coalesces bursts of changes (e.g. holding volume+) into a single write.</summary>
    public void SaveSoon()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void SaveNow()
    {
        _debounce.Stop();
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Config, JsonOptions));
            File.Move(tmp, FilePath, true);
        }
        catch
        {
            // Best effort: a failed save must never take the app down.
        }
    }
}
