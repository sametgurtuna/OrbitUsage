namespace Orbit.Models;

/// <summary>
/// Outcome of a single usage-fetch attempt for one service. Never thrown as an exception -
/// scraping/parsing failures are always represented as a failed result so callers can degrade
/// gracefully (keep showing the last known value) instead of crashing.
/// </summary>
public class UsageResult
{
    public bool Success { get; init; }
    public bool NotImplemented { get; init; }
    public double PercentUsed { get; init; }
    public string? RawText { get; init; }
    public string? ResetTimeText { get; init; }
    public double? SessionPercentUsed { get; init; }
    public string? SessionResetTimeText { get; init; }
    public bool HasSessionData => SessionPercentUsed.HasValue;
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static UsageResult Ok(
        double percentUsed,
        string? rawText = null,
        string? resetTimeText = null,
        double? sessionPercentUsed = null,
        string? sessionResetTimeText = null) => new()
    {
        Success = true,
        PercentUsed = percentUsed,
        RawText = rawText,
        ResetTimeText = resetTimeText,
        SessionPercentUsed = sessionPercentUsed,
        SessionResetTimeText = sessionResetTimeText
    };

    public static UsageResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static UsageResult NotImplementedResult(string message) => new()
    {
        Success = false,
        NotImplemented = true,
        ErrorMessage = message
    };
}
