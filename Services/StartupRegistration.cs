using Microsoft.Win32;

namespace TrimZero.Services;

/// <summary>
/// Per-user auto-start. The app starts minimized and waits for MSFS; no service, no
/// admin, no tray. The Run value is rewritten on every launch so moving the folder
/// keeps it working.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrimZero";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) return;
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        }
        catch
        {
            // Auto-start is a convenience; a locked registry is not worth a crash.
        }
    }
}
