using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Orbit.Models;

namespace Orbit.Services;

/// <summary>
/// Reads a usage percentage out of a locally-running Electron/Chromium desktop app (currently
/// Antigravity) via the Chrome DevTools Protocol, rather than Orbit's own WebView2 session. The
/// app must be launched with --remote-debugging-port={config.RemoteDebuggingPort} for this to see
/// anything at all - unlike a website, there's no "log in via Orbit's browser" flow, Orbit just
/// connects out to whatever's already running.
///
/// Flow: GET http://127.0.0.1:{port}/json lists open CDP targets (one per window/webview) →
/// pick the one matching config.TargetUrlContains (or the first "page" if unset) → open a
/// WebSocket to its webSocketDebuggerUrl → send a Runtime.evaluate JSON-RPC call running the same
/// querySelector/aria-valuenow/innerText extraction script SelectorUsageScraper uses for Claude →
/// parse the result the same way (PercentTextParser). Every failure path (port closed/refused, no
/// matching target, selector not found, malformed CDP response) degrades to UsageResult.Fail(...)
/// rather than throwing.
/// </summary>
internal static class ChromeDevToolsUsageScraper
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static async Task<UsageResult> ScrapeAsync(string serviceDisplayName, ServiceSelectorConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.UsageTextSelector))
            return UsageResult.Fail($"selectors.json has no usageTextSelector configured for {serviceDisplayName} yet - see its notes for how to fill this in");

        int port = config.RemoteDebuggingPort > 0 ? config.RemoteDebuggingPort : 9222;

        List<CdpTarget> targets;
        try
        {
            var json = await Http.GetStringAsync($"http://127.0.0.1:{port}/json", ct);
            targets = JsonSerializer.Deserialize<List<CdpTarget>>(json) ?? new();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UsageResult.Fail(
                $"{serviceDisplayName} not detected on port {port} - launch it with --remote-debugging-port={port} and try again ({ex.GetType().Name}: {ex.Message})");
        }

        var target = PickTarget(targets, config.TargetUrlContains);
        if (target?.WebSocketDebuggerUrl == null)
            return UsageResult.Fail($"No matching {serviceDisplayName} window found on port {port} (targetUrlContains: \"{config.TargetUrlContains}\")");

        try
        {
            var text = await EvaluateSelectorAsync(target.WebSocketDebuggerUrl, config.UsageTextSelector, ct);
            if (text == null)
                return UsageResult.Fail($"Selector not found in {serviceDisplayName}'s window - the selector may be out of date or the wrong panel is open");

            var percent = PercentTextParser.ExtractPercent(text, config.PercentRegex, config.InvertPercent);
            if (percent.HasValue)
            {
                string? resetText = null;
                if (!string.IsNullOrWhiteSpace(config.ResetTextSelector))
                {
                    var rawReset = await EvaluateSelectorAsync(target.WebSocketDebuggerUrl, config.ResetTextSelector, ct);
                    if (!string.IsNullOrWhiteSpace(rawReset))
                        resetText = rawReset.Trim();
                }
                return UsageResult.Ok(percent.Value, text, resetText);
            }

            return UsageResult.Fail($"Selector matched but no percentage found in text: \"{text}\"");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UsageResult.Fail($"CDP scraping error: {ex.Message}");
        }
    }

    private static CdpTarget? PickTarget(List<CdpTarget> targets, string? urlContains)
    {
        if (!string.IsNullOrWhiteSpace(urlContains))
            return targets.FirstOrDefault(t =>
                (t.Url?.Contains(urlContains, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Title?.Contains(urlContains, StringComparison.OrdinalIgnoreCase) ?? false));

        return targets.FirstOrDefault(t => t.Type == "page");
    }

    private static async Task<string?> EvaluateSelectorAsync(string webSocketDebuggerUrl, string selector, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(webSocketDebuggerUrl), ct);

        var escapedSelector = JsonSerializer.Serialize(selector);
        var expression = $$"""
            (function() {
              var el = document.querySelector({{escapedSelector}});
              if (!el) return null;
              var ariaValue = el.getAttribute('aria-valuenow');
              return ariaValue !== null ? ariaValue : (el.innerText || el.textContent || '');
            })()
            """;

        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            method = "Runtime.evaluate",
            @params = new { expression, returnByValue = true }
        });

        await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, ct);

        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);

        using var doc = JsonDocument.Parse(ms.ToArray());
        var root = doc.RootElement;
        if (!root.TryGetProperty("result", out var resultProp) ||
            !resultProp.TryGetProperty("result", out var valueWrapper) ||
            !valueWrapper.TryGetProperty("value", out var valueProp) ||
            valueProp.ValueKind != JsonValueKind.String)
            return null;

        return valueProp.GetString();
    }

    private class CdpTarget
    {
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("url")]
        public string? Url { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string? Title { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("webSocketDebuggerUrl")]
        public string? WebSocketDebuggerUrl { get; set; }
    }
}
