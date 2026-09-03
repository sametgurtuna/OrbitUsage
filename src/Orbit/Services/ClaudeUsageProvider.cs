using System.Text.Json;
using Orbit.Models;
using Microsoft.Web.WebView2.Core;

namespace Orbit.Services;

/// <summary>
/// Scrapes claude.ai's usage page via the shared WebView2 session.
/// Extracts both the Weekly Limit (All models) as the primary metric, and the Current Session
/// (5-hour limit) as secondary sub-orbit data, along with reset countdown timestamps.
/// </summary>
public class ClaudeUsageProvider : IUsageProvider
{
    public string ServiceKey => "Claude";
    public bool RequiresSharedWebView => true;

    public async Task<UsageResult> GetUsageAsync(CoreWebView2? webView, ServiceSelectorConfig config, CancellationToken ct)
    {
        if (webView == null)
            return UsageResult.Fail("WebView2 session not available for Claude");

        string url = string.IsNullOrWhiteSpace(config.UsageUrl) ? "https://claude.ai/settings/usage" : config.UsageUrl;

        try
        {
            bool navigated = await SelectorUsageScraper.NavigateAsync(webView, url, ct);
            if (!navigated)
                return UsageResult.Fail("Navigation to Claude usage page timed out or failed");

            // Extraction script that extracts both Weekly and Current Session meters & reset text
            const string extractionScript = """
                (function() {
                  try {
                    var meters = Array.from(document.querySelectorAll("[role='meter'][aria-valuenow]"));
                    if (meters.length === 0) {
                      return JSON.stringify({ ok: false, error: 'no meters found' });
                    }

                    function extractMeter(meterEl) {
                      if (!meterEl) return null;
                      var val = parseFloat(meterEl.getAttribute('aria-valuenow') || '0');
                      var resetText = '';
                      var p = meterEl.parentElement;
                      var depth = 0;
                      while (p && depth < 5) {
                        var text = p.innerText || p.textContent || '';
                        var match = text.match(/(?:resets?\s+(?:in|at)?|resets?)\s+([^\n\r.]+)/i);
                        if (match) {
                          resetText = match[1].trim();
                          break;
                        }
                        p = p.parentElement;
                        depth++;
                      }
                      return { val: val, resetText: resetText };
                    }

                    // On claude.ai/settings/usage:
                    // First meter: Current Session (5-hour)
                    // Second meter: Weekly Limit (All models)
                    var session = extractMeter(meters[0]);
                    var weekly = meters.length > 1 ? extractMeter(meters[1]) : session;

                    return JSON.stringify({
                      ok: true,
                      weeklyPercent: weekly ? weekly.val : (session ? session.val : 0),
                      weeklyReset: weekly ? weekly.resetText : '',
                      sessionPercent: session ? session.val : null,
                      sessionReset: session ? session.resetText : ''
                    });
                  } catch (e) {
                    return JSON.stringify({ ok: false, error: String(e && e.message || e) });
                  }
                })();
                """;

            for (int attempt = 0; attempt < Math.Max(1, config.MaxWaitAttempts); attempt++)
            {
                ct.ThrowIfCancellationRequested();

                string outerJson = await webView.ExecuteScriptAsync(extractionScript);
                if (!string.IsNullOrWhiteSpace(outerJson))
                {
                    string? inner = JsonSerializer.Deserialize<string>(outerJson);
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        using var doc = JsonDocument.Parse(inner);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
                        {
                            double weeklyPercent = root.TryGetProperty("weeklyPercent", out var wp) ? wp.GetDouble() : 0;
                            string? weeklyReset = root.TryGetProperty("weeklyReset", out var wr) ? wr.GetString() : null;
                            double? sessionPercent = root.TryGetProperty("sessionPercent", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetDouble() : null;
                            string? sessionReset = root.TryGetProperty("sessionReset", out var sr) ? sr.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(weeklyReset))
                                weeklyReset = weeklyReset.Trim();

                            if (!string.IsNullOrWhiteSpace(sessionReset))
                                sessionReset = sessionReset.Trim();

                            return UsageResult.Ok(
                                weeklyPercent,
                                $"{weeklyPercent:0}%",
                                weeklyReset,
                                sessionPercent,
                                sessionReset);
                        }
                    }
                }

                if (attempt < config.MaxWaitAttempts - 1)
                    await Task.Delay(Math.Max(300, config.WaitIntervalMs), ct);
            }

            // Fallback to standard single selector scraper if customized extraction didn't resolve
            return await SelectorUsageScraper.ScrapeAsync("Claude", webView, config, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UsageResult.Fail($"Claude scraping error: {ex.Message}");
        }
    }
}
