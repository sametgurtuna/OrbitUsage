namespace Orbit.Models;

public class AppSettings
{
    public int RefreshIntervalMinutes { get; set; } = 20;
    public NotchLayout Layout { get; set; } = NotchLayout.TopCenter;
    public bool StartWithWindows { get; set; } = false;

    /// <summary>DeviceName of the target monitor (e.g. from Screen.AllScreens). Null or empty targets the Primary monitor.</summary>
    public string? TargetMonitorDeviceName { get; set; }

    /// <summary>Fine-tune nudge (screen pixels) applied on top of the layout's default docked
    /// position. Positive X moves right, positive Y moves down, for both layouts.</summary>
    public double NotchOffsetX { get; set; } = 0;
    public double NotchOffsetY { get; set; } = 0;

    /// <summary>How fast the notch grows/shrinks on hover. See <see cref="NotchAnimationSpeed"/>.</summary>
    public NotchAnimationSpeed AnimationSpeed { get; set; } = NotchAnimationSpeed.Normal;

    public Dictionary<string, ServiceSettings> Services { get; set; } = new()
    {
        ["Claude"] = new ServiceSettings { Enabled = true, ManualMode = false },
        ["ChatGPT"] = new ServiceSettings { Enabled = false, ManualMode = false },
        // Enabled by default so the gauge is visible from first run; ManualMode defaults to true
        // since AntigravityUsageProvider's CDP selector isn't verified against a live session yet
        // (see selectors.json notes) - manual is the reliable starting point until it's filled in.
        ["Antigravity"] = new ServiceSettings { Enabled = true, ManualMode = true },
    };
}
