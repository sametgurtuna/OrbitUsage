namespace Orbit.ViewModels;

/// <summary>Drives the notch's status-dot color for one service row.</summary>
public enum UsageStatus
{
    Normal,     // < 80% used
    Warning,    // >= 80% used
    Critical,   // >= 95% used
    Unavailable,// last scrape failed, showing stale/last-known value (or no value yet)
    NotImplemented // provider stub - service not wired up yet (ChatGPT/Gemini in Phase 1)
}
