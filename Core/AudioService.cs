using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace PincabVolume;

/// <summary>Per-device master volume through the Core Audio API (no admin rights needed).</summary>
public static class AudioService
{
    // All calls happen on the UI thread, so one enumerator can be reused.
    static readonly MMDeviceEnumerator Enumerator = new();

    public static List<AudioDeviceInfo> GetDevices()
    {
        var list = new List<AudioDeviceInfo> { new("", "Default output device") };
        foreach (var d in Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            using (d)
                list.Add(new AudioDeviceInfo(d.ID, d.FriendlyName));
        return list;
    }

    static MMDevice Open(string id) => string.IsNullOrEmpty(id)
        ? Enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
        : Enumerator.GetDevice(id);

    public static int? GetVolume(string id)
    {
        try
        {
            using var d = Open(id);
            return (int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
        }
        catch
        {
            return null; // device unplugged / disabled
        }
    }

    public static void SetVolume(string id, int percent)
    {
        try
        {
            using var d = Open(id);
            d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f;
        }
        catch
        {
            // device unplugged / disabled
        }
    }
}
