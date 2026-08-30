using System.IO;
using System.Text.Json;
using Orbit.Models;

namespace Orbit.Services;

/// <summary>
/// Loads selectors.json from %LOCALAPPDATA%\Orbit\selectors.json, seeding it from the
/// bundled copy on first run so the user can hand-edit it afterwards without rebuilding.
/// </summary>
public class SelectorConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string SelectorsPath { get; }
    private readonly string _bundledPath;

    public SelectorConfig Current { get; private set; } = new();

    public SelectorConfigService()
    {
        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Orbit");
        Directory.CreateDirectory(appDataDir);
        SelectorsPath = Path.Combine(appDataDir, "selectors.json");
        _bundledPath = Path.Combine(AppContext.BaseDirectory, "Resources", "selectors.json");
    }

    public SelectorConfig Load()
    {
        try
        {
            if (!File.Exists(SelectorsPath) && File.Exists(_bundledPath))
                File.Copy(_bundledPath, SelectorsPath);

            if (File.Exists(SelectorsPath))
            {
                var json = File.ReadAllText(SelectorsPath);
                var config = JsonSerializer.Deserialize<SelectorConfig>(json);
                if (config != null)
                {
                    Current = config;
                    return Current;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable file - fall through to bundled defaults so the app never
            // fails to start because of a hand-edit gone wrong.
        }

        Current = LoadBundledFallback();
        return Current;
    }

    /// <summary>Re-reads selectors.json from disk (e.g. "Reload selectors" button in Settings).</summary>
    public SelectorConfig Reload() => Load();

    private SelectorConfig LoadBundledFallback()
    {
        try
        {
            if (File.Exists(_bundledPath))
            {
                var json = File.ReadAllText(_bundledPath);
                var config = JsonSerializer.Deserialize<SelectorConfig>(json);
                if (config != null) return config;
            }
        }
        catch
        {
            // fall through to empty config below
        }

        return new SelectorConfig();
    }
}
