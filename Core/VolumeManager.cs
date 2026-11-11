using System;
using System.Collections.Generic;
using System.Linq;
using PincabVolume.Views;

namespace PincabVolume;

/// <summary>Maps bindings to volume changes and remembers the volume per table and per profile.</summary>
public sealed class VolumeManager
{
    /// <summary>Bucket used when no table is running.</summary>
    public const string SystemKey = "__system__";

    readonly ConfigService _store;
    readonly InputService _input;
    readonly OsdWindow _osd;

    public VolumeManager(ConfigService store, InputService input, OsdWindow osd)
    {
        _store = store;
        _input = input;
        _osd = osd;
    }

    AppConfig Config => _store.Config;
    public string CurrentTable { get; private set; }
    public event Action ContextChanged;

    public Profile ActiveProfile =>
        Config.Profiles.FirstOrDefault(p => p.Id == Config.ActiveProfileId) ?? Config.Profiles.FirstOrDefault();

    Dictionary<string, int> Bucket(string key)
    {
        if (!Config.TableVolumes.TryGetValue(key, out var bucket)) Config.TableVolumes[key] = bucket = new();
        return bucket;
    }

    /// <summary>
    /// Restores every profile's volume for the current context:
    /// saved table volume → profile's table default (if enabled, tables only) → System volume.
    /// </summary>
    public void ApplyContext()
    {
        var system = Bucket(SystemKey);
        var table = CurrentTable != null ? Config.TableVolumes.GetValueOrDefault(CurrentTable) : null;

        foreach (var p in Config.Profiles)
        {
            // First time we see a profile: adopt its current device volume as the System volume.
            if (!system.ContainsKey(p.Id) && AudioService.GetVolume(p.DeviceId) is int current) system[p.Id] = current;

            int? volume = null;
            if (table != null && table.TryGetValue(p.Id, out var saved)) volume = saved;
            else if (CurrentTable != null && p.UseDefaultVolume) volume = p.DefaultVolume;
            else if (system.TryGetValue(p.Id, out var sys)) volume = sys;

            if (volume is int v) AudioService.SetVolume(p.DeviceId, Math.Min(v, p.MaxVolume));
        }

        _store.SaveSoon();
    }

    public void OnTableChanged(string table)
    {
        CurrentTable = table;
        ApplyContext();
        ContextChanged?.Invoke();
        if (ActiveProfile is { } p && AudioService.GetVolume(p.DeviceId) is int v) _osd.ShowVolume(p.Name, v, CurrentTable);
    }

    public void Change(Profile p, int delta)
    {
        if (AudioService.GetVolume(p.DeviceId) is not int current) return;
        var next = Math.Clamp(current + delta, 0, p.MaxVolume);
        AudioService.SetVolume(p.DeviceId, next);
        Bucket(CurrentTable ?? SystemKey)[p.Id] = next;
        _store.SaveSoon();
        _osd.ShowVolume(p.Name, next, CurrentTable);
    }

    public void OnBinding(string binding)
    {
        if (string.IsNullOrEmpty(binding)) return;

        // Profiles with a hold key win while that key is held: "hold + rest" must match exactly.
        var pressed = InputService.Tokens(binding);
        bool handled = false;
        foreach (var p in Config.Profiles.Where(p => !string.IsNullOrEmpty(p.HoldKey)))
        {
            var hold = InputService.Tokens(p.HoldKey);
            if (!hold.All(_input.IsHeld)) continue;

            var rest = pressed.Except(hold).ToArray();
            if (rest.Length == 0) continue; // only the hold key itself was pressed

            if (SameTokens(rest, p.VolUp, hold)) Change(p, p.Step);
            else if (SameTokens(rest, p.VolDown, hold)) Change(p, -p.Step);
            else continue;
            handled = true;
        }

        if (handled) return;

        var target = Config.Profiles.FirstOrDefault(p =>
            string.IsNullOrEmpty(p.HoldKey) && (p.VolUp == binding || p.VolDown == binding));
        if (target != null) Change(target, target.VolUp == binding ? target.Step : -target.Step);
    }

    /// <summary>
    /// True when <paramref name="tokens"/> equal the binding's tokens, ignoring the hold key's tokens:
    /// a gamepad binding captured while holding the hold button ("Start + A") still means "A".
    /// </summary>
    static bool SameTokens(string[] tokens, string binding, string[] holdTokens)
    {
        if (string.IsNullOrEmpty(binding)) return false;
        var expected = InputService.Tokens(binding).Except(holdTokens).ToArray();
        return expected.Length == tokens.Length && !expected.Except(tokens).Any();
    }
}
