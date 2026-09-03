namespace Orbit.Models;

public class ServiceSettings
{
    public bool Enabled { get; set; }
    public bool ManualMode { get; set; }
    public double ManualPercent { get; set; }
    public double LastKnownPercent { get; set; }
    public string? LastKnownResetText { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public string? LastError { get; set; }

    /// <summary>Last known 5-hour ("session") window quota, e.g. Antigravity's Five Hour Limit.
    /// Persisted separately from the weekly LastKnownPercent above so the 5h gauge is populated
    /// immediately on startup instead of only after the first live refresh completes.</summary>
    public double? LastKnownSessionPercent { get; set; }
    public string? LastKnownSessionResetText { get; set; }

    public bool NotifyAt80 { get; set; } = true;
    public bool NotifyAt95 { get; set; } = true;
    public bool NotifyAt100 { get; set; } = true;
    public bool NotifyOnReset { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public int LastNotifiedThreshold { get; set; }
}
