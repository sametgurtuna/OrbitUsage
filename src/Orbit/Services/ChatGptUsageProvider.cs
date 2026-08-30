using Orbit.Models;
using Microsoft.Web.WebView2.Core;

namespace Orbit.Services;

/// <summary>Stub - Phase 1 scope is Claude only. Wire up like ClaudeUsageProvider once selectors.json's
/// ChatGPT entry is filled in.</summary>
public class ChatGptUsageProvider : IUsageProvider
{
    public string ServiceKey => "ChatGPT";
    public bool RequiresSharedWebView => true;

    public Task<UsageResult> GetUsageAsync(CoreWebView2? webView, ServiceSelectorConfig config, CancellationToken ct) =>
        Task.FromResult(UsageResult.NotImplementedResult("ChatGPT scraping not yet implemented"));
}
