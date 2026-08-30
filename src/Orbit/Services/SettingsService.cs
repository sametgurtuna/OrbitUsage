using System.IO;
using System.Text.Json;
using Orbit.Models;

namespace Orbit.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string SettingsPath { get; }
    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Orbit");
        Directory.CreateDirectory(appDataDir);
        SettingsPath = Path.Combine(appDataDir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            Current = new AppSettings();
            Save(Current);
            return Current;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            Current = settings ?? new AppSettings();
        }
        catch
        {
            // Corrupt settings file - back it up and fall back to defaults rather than fail startup.
            try
            {
                File.Copy(SettingsPath, SettingsPath + ".bak", overwrite: true);
            }
            catch
            {
                // best-effort backup only
            }
            Current = new AppSettings();
            Save(Current);
        }

        return Current;
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence - a failed write shouldn't crash a background refresh tick.
        }
    }
}
