using System.IO;
using System.Text.Json;

namespace TrimZero.Core;

public sealed class AppSettings
{
    public int Version { get; set; } = 2;
    public string DeviceProduct { get; set; } = "";
    public string AxisName { get; set; } = "X Axis";
    public bool Invert { get; set; }
    public double WheelMin { get; set; } = 0;
    public double WheelMax { get; set; } = 65535;
    public double Sensitivity { get; set; } = 1.0;
    public bool Topmost { get; set; } = true;
    public double? MarkWheel { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public Dictionary<string, bool> AircraftInvert { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> AircraftTakeoff { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrimZero");

    public static string PathName => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(PathName)) return new AppSettings();
            var json = File.ReadAllText(PathName);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, PathName, overwrite: true);
        }
        catch
        {
            // Settings are a convenience; never let a failed write take the app down.
        }
    }
}
