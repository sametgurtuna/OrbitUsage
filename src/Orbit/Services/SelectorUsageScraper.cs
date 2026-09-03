using System.Text.Json;
using Orbit.Models;
using Microsoft.Web.WebView2.Core;

namespace Orbit.Services;

/// <summary>
/// Shared selector-driven scraping logic used by every IUsageProvider that reads a usage
/// percentage off a page via the shared WebView2 session (Claude, Gemini, and - once wired up -
/// ChatGPT). None of this is provider-specific: the URL, wait/text selectors, and percent regex
/// all come from selectors.json, so providers themselves are thin wrappers that just supply a
/// display name for error messages. Every failure path degrades to UsageResult.Fail(...) rather
/// than throwing, so a broken or unconfigured selector never crashes the app.
/// </summary>
internal static class SelectorUsageScraper
{
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(15);

    public static async Task<UsageResult> ScrapeAsync(
        string serviceDisplayName, CoreWebView2 webView, ServiceSelectorConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.UsageUrl) || string.IsNullOrWhiteSpace(config.UsageTextSelector))
            return UsageResult.Fail($"selectors.json has no usageUrl/usageTextSelector configured for {serviceDisplayName}");

        try
        {
            var navigated = await NavigateAsync(webView, config.UsageUrl, ct);
            if (!navigated)
                return UsageResult.Fail($"Navigation to {serviceDisplayName} usage page timed out or failed");

            for (int attempt = 0; attempt < Math.Max(1, config.MaxWaitAttempts); attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var (ok, text, error) = await TryExtractAsync(webView, config.UsageTextSelector);
                if (ok && text != null)
                {
                    var percent = PercentTextParser.ExtractPercent(text, config.PercentRegex, config.InvertPercent);
                    if (percent.HasValue)
                    {
                        string? resetText = null;
                        if (!string.IsNullOrWhiteSpace(config.ResetTextSelector))
                        {
                            var (resetOk, rawReset, _) = await TryExtractAsync(webView, config.ResetTextSelector);
                            if (resetOk && !string.IsNullOrWhiteSpace(rawReset))
                                resetText = rawReset.Trim();
                        }
                        return UsageResult.Ok(percent.Value, text, resetText);
                    }

                    return UsageResult.Fail($"Selector matched but no percentage found in text: \"{text}\"");
                }

                if (attempt < config.MaxWaitAttempts - 1)
                    await Task.Delay(Math.Max(200, config.WaitIntervalMs), ct);
                else if (error != null)
                    return UsageResult.Fail(error);
            }

            return UsageResult.Fail("usageTextSelector not found after retries - the selector may be out of date, see selectors.json notes");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UsageResult.Fail($"Unexpected scraping error: {ex.Message}");
        }
    }

    internal static async Task<bool> NavigateAsync(CoreWebView2 webView, string url, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);
        webView.NavigationCompleted += Handler;
        try
        {
            webView.Navigate(url);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(NavigationTimeout, ct));
            return completed == tcs.Task && tcs.Task.Result;
        }
        finally
        {
            webView.NavigationCompleted -= Handler;
        }
    }

    private static async Task<(bool ok, string? text, string? error)> TryExtractAsync(CoreWebView2 webView, string selector)
    {
        // The extraction script itself returns a JSON string ({ok, text} or {ok:false, error}),
        // so ExecuteScriptAsync's result is a JSON-encoded string of that JSON string - decode twice.
        var escapedSelector = JsonSerializer.Serialize(selector);
        var script = $$"""
            (function() {
              try {
                var el = document.querySelector({{escapedSelector}});
                if (!el) return JSON.stringify({ ok: false, error: 'selector not found' });
                // Prefer a direct ARIA value (e.g. role="meter" aria-valuenow="22") - a clean
                // 0-100 number, more robust than parsing visible text. Fall back to text content.
                var ariaValue = el.getAttribute('aria-valuenow');
                var text = ariaValue !== null ? ariaValue : (el.innerText || el.textContent || '');
                return JSON.stringify({ ok: true, text: text });
              } catch (e) {
                return JSON.stringify({ ok: false, error: String(e && e.message || e) });
              }
            })();
            """;

        string outerJson;
        try
        {
            outerJson = await webView.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            return (false, null, $"ExecuteScriptAsync failed: {ex.Message}");
        }

        try
        {
            var inner = JsonSerializer.Deserialize<string>(outerJson);
            if (inner == null) return (false, null, "empty script result");

            using var doc = JsonDocument.Parse(inner);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
            if (ok && root.TryGetProperty("text", out var textProp))
                return (true, textProp.GetString(), null);

            var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "unknown extraction error";
            return (false, null, error);
        }
        catch (JsonException ex)
        {
            return (false, null, $"Could not parse script result: {ex.Message}");
        }
    }
}
