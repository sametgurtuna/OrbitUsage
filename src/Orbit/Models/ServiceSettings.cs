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

    public bool NotifyAt80 { get; set; } = true;
    public bool NotifyAt95 { get; set; } = true;
    public bool NotifyAt100 { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public int LastNotifiedThreshold { get; set; }
}
