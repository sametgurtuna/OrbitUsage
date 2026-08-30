using System.Text.RegularExpressions;

namespace Orbit.Services;

/// <summary>
/// Shared "extract a 0-100 percentage from arbitrary scraped text via a configurable regex" logic,
/// used by every scraper regardless of transport (WebView2's SelectorUsageScraper, the CDP-based
/// ChromeDevToolsUsageScraper) - none of this depends on how the text was fetched.
/// </summary>
internal static class PercentTextParser
{
    public static double? ExtractPercent(string text, string pattern, bool invert = false)
    {
        try
        {
            var match = Regex.Match(text, string.IsNullOrWhiteSpace(pattern) ? @"(\d{1,3})\s*%" : pattern);
            if (match.Success && match.Groups.Count > 1 && double.TryParse(match.Groups[1].Value, out var value))
            {
                if (invert) value = 100 - value;
                return Math.Clamp(value, 0, 100);
            }
        }
        catch (RegexParseException)
        {
            // malformed percentRegex in selectors.json - treat as "no match", surfaced as a Fail() by the caller
        }
        return null;
    }
}
