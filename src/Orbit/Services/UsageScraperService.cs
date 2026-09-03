using System.Windows.Threading;
using Orbit.Models;

namespace Orbit.Services;

/// <summary>
/// Polls each enabled IUsageProvider on a DispatcherTimer (must run on the UI thread - WebView2
/// controls are thread-affine), applies manual-mode overrides, and persists results via
/// SettingsService. Every provider call failure degrades to "keep last known value" rather than
/// propagating - this service never lets a scrape failure crash or hang the app.
/// </summary>
public class UsageScraperService
{
    private readonly WebView2SessionManager _session;
    private readonly SettingsService _settingsService;
    private readonly SelectorConfigService _selectorService;
    private readonly IReadOnlyDictionary<string, IUsageProvider> _providers;
    private readonly DispatcherTimer _timer = new();
    private bool _refreshInFlight;

    public event Action<string, UsageResult>? UsageUpdated;
    public event Action<bool>? RefreshingChanged;

    public UsageScraperService(
        WebView2SessionManager session,
        SettingsService settingsService,
        SelectorConfigService selectorService,
        IEnumerable<IUsageProvider> providers)
    {
        _session = session;
        _settingsService = settingsService;
        _selectorService = selectorService;
        _providers = providers.ToDictionary(p => p.ServiceKey, p => p);

        _timer.Tick += async (_, _) => await RefreshNowAsync();
    }

    public void Start()
    {
        _timer.Interval = TimeSpan.FromMinutes(Math.Max(1, _settingsService.Current.RefreshIntervalMinutes));
        _timer.Start();
    }

    /// <summary>Applies a changed refresh interval immediately without restarting the whole service.</summary>
    public void ApplyIntervalChange()
    {
        _timer.Stop();
        _timer.Interval = TimeSpan.FromMinutes(Math.Max(1, _settingsService.Current.RefreshIntervalMinutes));
        _timer.Start();
    }

    public async Task RefreshNowAsync(string? onlyServiceKey = null)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshNowAsync(onlyServiceKey));
            return;
        }

        if (_refreshInFlight) return;
        _refreshInFlight = true;
        RefreshingChanged?.Invoke(true);

        try
        {
            var settings = _settingsService.Current;
            var selectors = _selectorService.Current;

            foreach (var (key, provider) in _providers)
            {
                if (onlyServiceKey != null && key != onlyServiceKey) continue;

                if (!settings.Services.TryGetValue(key, out var serviceSettings))
                {
                    serviceSettings = new ServiceSettings();
                    settings.Services[key] = serviceSettings;
                }

                if (!serviceSettings.Enabled) continue;

                var result = await RefreshOneAsync(provider, serviceSettings, selectors);
                UsageUpdated?.Invoke(key, result);
            }

            _settingsService.Save(settings);
        }
        finally
        {
            _refreshInFlight = false;
            RefreshingChanged?.Invoke(false);
        }
    }

    private async Task<UsageResult> RefreshOneAsync(IUsageProvider provider, ServiceSettings serviceSettings, SelectorConfig selectors)
    {
        if (serviceSettings.ManualMode)
        {
            serviceSettings.LastKnownPercent = serviceSettings.ManualPercent;
            serviceSettings.LastUpdatedUtc = DateTime.UtcNow;
            serviceSettings.LastError = null;
            return UsageResult.Ok(serviceSettings.ManualPercent);
        }

        try
        {
            Microsoft.Web.WebView2.Core.CoreWebView2? webView = null;
            bool sessionAcquired = false;

            if (provider.RequiresSharedWebView)
            {
                if (_session.IsInteractiveSessionActive)
                {
                    serviceSettings.LastError = "Sign-in in progress, scrape skipped";
                    return UsageResult.Fail(serviceSettings.LastError);
                }

                sessionAcquired = await _session.TryAcquireSessionAsync();
                if (!sessionAcquired)
                {
                    serviceSettings.LastError = "WebView session is currently busy";
                    return UsageResult.Fail(serviceSettings.LastError);
                }

                var initialized = await _session.InitializeAsync();
                if (!initialized)
                {
                    _session.ReleaseSession();
                    serviceSettings.LastError = "WebView2 Runtime not available - switch this service to manual mode or install the runtime";
                    return UsageResult.Fail(serviceSettings.LastError);
                }
                webView = _session.SharedWebView.CoreWebView2;
            }

            try
            {
                selectors.Services.TryGetValue(provider.ServiceKey, out var config);
                config ??= new ServiceSelectorConfig();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                var result = await provider.GetUsageAsync(webView, config, cts.Token);

                if (result.Success)
                {
                    serviceSettings.LastKnownPercent = result.PercentUsed;
                    serviceSettings.LastKnownResetText = result.ResetTimeText;
                    serviceSettings.LastUpdatedUtc = DateTime.UtcNow;
                    serviceSettings.LastError = null;
                    if (result.HasSessionData)
                    {
                        serviceSettings.LastKnownSessionPercent = result.SessionPercentUsed;
                        serviceSettings.LastKnownSessionResetText = result.SessionResetTimeText;
                    }
                    Serilog.Log.Information("[UsageScraper] {Service} refreshed: {Percent}% (Reset: {Reset})", provider.ServiceKey, result.PercentUsed, result.ResetTimeText ?? "none");
                }
                else if (!result.NotImplemented)
                {
                    // Keep LastKnownPercent as-is - show stale data rather than nothing.
                    serviceSettings.LastError = result.ErrorMessage;
                    Serilog.Log.Warning("[UsageScraper] {Service} refresh failed: {Error}", provider.ServiceKey, result.ErrorMessage);
                }

                return result;
            }
            finally
            {
                if (sessionAcquired)
                {
                    _session.ReleaseSession();
                }
            }
        }
        catch (Exception ex)
        {
            // Belt-and-suspenders: providers are documented to never throw, but a scrape must
            // never be able to bring down the polling loop regardless.
            serviceSettings.LastError = $"Unexpected error: {ex.Message}";
            Serilog.Log.Error(ex, "[UsageScraper] Unexpected error scraping {Service}", provider.ServiceKey);
            return UsageResult.Fail(serviceSettings.LastError);
        }
    }
}
