using Orbit.Models;
using Microsoft.Web.WebView2.Core;

namespace Orbit.Services;

/// <summary>
/// Scrapes claude.ai's usage page via the shared, isolated WebView2 session. All the actual
/// navigate/wait/extract/parse logic lives in SelectorUsageScraper (shared with every other
/// selector-driven provider) since none of it is Claude-specific - everything that could differ
/// per service is externalized to selectors.json.
/// </summary>
public class ClaudeUsageProvider : IUsageProvider
{
    public string ServiceKey => "Claude";
    public bool RequiresSharedWebView => true;

    public Task<UsageResult> GetUsageAsync(CoreWebView2? webView, ServiceSelectorConfig config, CancellationToken ct) =>
        SelectorUsageScraper.ScrapeAsync("Claude", webView!, config, ct);
}
