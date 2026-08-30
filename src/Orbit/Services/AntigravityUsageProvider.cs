using Orbit.Models;
using Microsoft.Web.WebView2.Core;

namespace Orbit.Services;

/// <summary>
/// Reads Google Antigravity's (the desktop agentic IDE, not the Gemini web app - they track
/// separate usage quotas) usage percentage. Unlike Claude, Antigravity isn't a web page Orbit can
/// navigate in its own browser session - it's a local Electron/VS Code-fork app whose "Quota
/// available" bars live in its own Settings > Models screen. This reads that via the Chrome
/// DevTools Protocol (see ChromeDevToolsUsageScraper), which only works while Antigravity is
/// running and was launched with --remote-debugging-port (see selectors.json's Antigravity notes
/// for the exact setup and how to find the right selector using Settings' "Detect Antigravity..."
/// button). Until that selector is filled in and verified against a live session, this degrades
/// gracefully to UsageResult.Fail(...) ("Data unavailable") - Manual mode is the default and the
/// reliable fallback in the meantime.
/// </summary>
public class AntigravityUsageProvider : IUsageProvider
{
    public string ServiceKey => "Antigravity";
    public bool RequiresSharedWebView => false;

    public Task<UsageResult> GetUsageAsync(CoreWebView2? webView, ServiceSelectorConfig config, CancellationToken ct) =>
        ChromeDevToolsUsageScraper.ScrapeAsync("Antigravity", config, ct);
}
