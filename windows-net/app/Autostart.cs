// "Open at login" — the per-user Run key, under the same value name the Tauri
// build's autostart plugin used, so switching builds replaces the old entry
// instead of leaving a second one behind.

using Microsoft.Win32;

namespace Coucou;

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Coucou";

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception e)
        {
            Log.Line($"autostart: {e.Message}");
        }
    }
}
