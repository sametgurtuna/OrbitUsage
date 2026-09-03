namespace Orbit.Models;

public class AppSettings
{
    public int RefreshIntervalMinutes { get; set; } = 20;
    public NotchLayout Layout { get; set; } = NotchLayout.RightCenter;
    public NotchTheme Theme { get; set; } = NotchTheme.Dark;
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Whether the notch window stays Topmost above all windows. If false, it stays on the desktop level.</summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>Whether the global keyboard shortcut is active to expand/collapse Orbit.</summary>
    public bool HotkeyEnabled { get; set; } = true;

    /// <summary>Modifiers for the global hotkey (e.g. "Win+Alt", "Ctrl+Alt", "Ctrl+Shift", "Alt+Shift").</summary>
    public string HotkeyModifiers { get; set; } = "Win+Alt";

    /// <summary>Key for the global hotkey (e.g. "O", "Space").</summary>
    public string HotkeyKey { get; set; } = "O";

    /// <summary>DeviceName of the target monitor (e.g. from Screen.AllScreens). Null or empty targets the Primary monitor.</summary>
    public string? TargetMonitorDeviceName { get; set; }

    /// <summary>Fine-tune nudge (screen pixels) applied on top of the layout's default docked
    /// position. Positive X moves right, positive Y moves down, for both layouts.</summary>
    public double NotchOffsetX { get; set; } = 0;
    public double NotchOffsetY { get; set; } = 0;

    /// <summary>How fast the notch grows/shrinks on hover. See <see cref="NotchAnimationSpeed"/>.</summary>
    public NotchAnimationSpeed AnimationSpeed { get; set; } = NotchAnimationSpeed.Normal;

    /// <summary>Whether to host a lightweight local REST API on localhost for Stream Deck, Rainmeter, and CLI scripts.</summary>
    public bool EnableLocalApi { get; set; } = true;

    /// <summary>Port for the local REST API server (default: 18923).</summary>
    public int LocalApiPort { get; set; } = 18923;

    public Dictionary<string, ServiceSettings> Services { get; set; } = new()
    {
        ["Claude"] = new ServiceSettings { Enabled = true, ManualMode = false },
        ["Antigravity"] = new ServiceSettings { Enabled = true, ManualMode = false },
        ["ChatGPT"] = new ServiceSettings { Enabled = false, ManualMode = false },
    };
}
