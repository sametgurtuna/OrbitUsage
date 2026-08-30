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
            if (provider.RequiresSharedWebView)
            {
                var initialized = await _session.InitializeAsync();
                if (!initialized)
                {
                    serviceSettings.LastError = "WebView2 Runtime not available - switch this service to manual mode or install the runtime";
                    return UsageResult.Fail(serviceSettings.LastError);
                }
                webView = _session.SharedWebView.CoreWebView2;
            }

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
            }
            else if (!result.NotImplemented)
            {
                // Keep LastKnownPercent as-is - show stale data rather than nothing.
                serviceSettings.LastError = result.ErrorMessage;
            }

            return result;
        }
        catch (Exception ex)
        {
            // Belt-and-suspenders: providers are documented to never throw, but a scrape must
            // never be able to bring down the polling loop regardless.
            serviceSettings.LastError = $"Unexpected error: {ex.Message}";
            return UsageResult.Fail(serviceSettings.LastError);
        }
    }
}
