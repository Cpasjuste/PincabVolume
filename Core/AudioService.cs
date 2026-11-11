using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;

namespace PincabVolume;

/// <summary>Per-device master volume through the Core Audio API (no admin rights needed).</summary>
public static class AudioService
{
    // All calls happen on the UI thread, so one enumerator can be reused.
    static readonly MMDeviceEnumerator Enumerator = new();

    // Windows renames re-enumerated endpoints: "Speakers (USB Audio)" -> "Speakers (2- USB Audio)".
    static readonly Regex InstancePrefix = new(@"\(\d+-\s*", RegexOptions.Compiled);

    public static List<AudioDeviceInfo> GetDevices()
    {
        var list = new List<AudioDeviceInfo> { new("", "Default output device") };
        foreach (var d in Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            using (d)
                list.Add(new AudioDeviceInfo(d.ID, d.FriendlyName));
        return list;
    }

    /// <summary>
    /// Finds the profile's device: by id when it is present, otherwise by name (USB cards can get a new id
    /// after a reboot or a Windows update). Self-heals the profile's stored id / name. Null when not found.
    /// </summary>
    static MMDevice Open(Profile p)
    {
        if (string.IsNullOrEmpty(p.DeviceId))
            return Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        try
        {
            var byId = Enumerator.GetDevice(p.DeviceId);
            if (byId.State == DeviceState.Active)
            {
                p.DeviceName = byId.FriendlyName; // remember the name for a later fallback
                return byId;
            }

            byId.Dispose();
        }
        catch
        {
            // unknown id: fall through to the name lookup
        }

        if (string.IsNullOrEmpty(p.DeviceName)) return null;

        // An exact name wins over a merely renumbered one ("2- ").
        var key = NormalizeName(p.DeviceName);
        MMDevice exact = null, renumbered = null;
        foreach (var d in Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if (exact == null && d.FriendlyName == p.DeviceName) exact = d;
            else if (renumbered == null && NormalizeName(d.FriendlyName) == key) renumbered = d;
            else d.Dispose();
        }

        var match = exact ?? renumbered;
        if (exact != null) renumbered?.Dispose();

        if (match != null)
        {
            p.DeviceId = match.ID;
            p.DeviceName = match.FriendlyName;
        }

        return match;
    }

    static string NormalizeName(string name) =>
        InstancePrefix.Replace(name ?? "", "(").Trim().ToLowerInvariant();

    /// <summary>Re-resolves the profile's device (updating a changed id). False when it is not connected.</summary>
    public static bool Resolve(Profile p)
    {
        try
        {
            using var d = Open(p);
            return d != null;
        }
        catch
        {
            return false;
        }
    }

    public static int? GetVolume(Profile p)
    {
        try
        {
            using var d = Open(p);
            return d == null ? null : (int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
        }
        catch
        {
            return null; // device unplugged / disabled
        }
    }

    public static void SetVolume(Profile p, int percent)
    {
        try
        {
            using var d = Open(p);
            if (d != null) d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f;
        }
        catch
        {
            // device unplugged / disabled
        }
    }
}
