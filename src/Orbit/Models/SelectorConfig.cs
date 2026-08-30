using System.Text.Json.Serialization;

namespace Orbit.Models;

public class SelectorConfig
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceSelectorConfig> Services { get; set; } = new();
}

public class ServiceSelectorConfig
{
    [JsonPropertyName("usageUrl")]
    public string UsageUrl { get; set; } = "";

    [JsonPropertyName("waitForSelector")]
    public string WaitForSelector { get; set; } = "";

    [JsonPropertyName("usageTextSelector")]
    public string UsageTextSelector { get; set; } = "";

    [JsonPropertyName("percentRegex")]
    public string PercentRegex { get; set; } = @"(\d{1,3})\s*%";

    [JsonPropertyName("resetTextSelector")]
    public string? ResetTextSelector { get; set; }

    [JsonPropertyName("resetTextRegex")]
    public string? ResetTextRegex { get; set; }

    [JsonPropertyName("maxWaitAttempts")]
    public int MaxWaitAttempts { get; set; } = 5;

    [JsonPropertyName("waitIntervalMs")]
    public int WaitIntervalMs { get; set; } = 1000;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>Chrome DevTools Protocol port for local-app providers (e.g. Antigravity, launched
    /// with --remote-debugging-port). Unused by WebView2-based providers like Claude.</summary>
    [JsonPropertyName("remoteDebuggingPort")]
    public int RemoteDebuggingPort { get; set; } = 9222;

    /// <summary>Substring to match against a CDP target's url/title to pick the right window/webview
    /// out of the debugged app's possibly-many open targets. Empty matches the first "page" target.</summary>
    [JsonPropertyName("targetUrlContains")]
    public string? TargetUrlContains { get; set; }

    /// <summary>Set true when the scraped number is "% remaining" rather than "% used" (e.g.
    /// Antigravity's "Weekly Limit Remaining" figure) - the extracted value is stored as
    /// 100 - value so PercentUsed keeps its usual meaning (and the color thresholds/gauge still
    /// make sense: full quota remaining should read as low usage, not critical).</summary>
    [JsonPropertyName("invertPercent")]
    public bool InvertPercent { get; set; }
}
