using System;
using Microsoft.Win32;

namespace PincabVolume;

/// <summary>Per-user "run at logon" through HKCU\...\Run (no admin rights needed).</summary>
public static class StartupService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "PincabVolume";

    public static void Set(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (enable) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(ValueName, false);
        }
        catch
        {
            // Policy may forbid it; nothing useful to do.
        }
    }
}
