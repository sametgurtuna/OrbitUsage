using Orbit.Models;
using Microsoft.Web.WebView2.Core;

namespace Orbit.Services;

public interface IUsageProvider
{
    /// <summary>Matches AppSettings.Services / SelectorConfig.Services keys ("Claude", "ChatGPT", "Antigravity").</summary>
    string ServiceKey { get; }

    /// <summary>
    /// Whether this provider needs Orbit's shared, isolated WebView2 session (true for
    /// page-scraping providers like Claude/ChatGPT). False for providers that talk to something
    /// else entirely (e.g. Antigravity, which connects out to a local Chrome DevTools Protocol
    /// port rather than using Orbit's own browser session) - UsageScraperService skips WebView2
    /// initialization for those and passes null for webView.
    /// </summary>
    bool RequiresSharedWebView { get; }

    /// <summary>
    /// Fetches current usage. Must never throw - all failure modes (navigation timeout, selector
    /// not found, JS error, not-yet-implemented) are represented via the returned UsageResult.
    /// webView is null when RequiresSharedWebView is false.
    /// </summary>
    Task<UsageResult> GetUsageAsync(CoreWebView2? webView, ServiceSelectorConfig config, CancellationToken ct);
}
