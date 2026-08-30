using System.Windows.Forms;
using Orbit.Models;

namespace Orbit.Services;

/// <summary>
/// Monitors usage changes across services and dispatches Windows balloon/toast notifications
/// when quota percentages cross configured warning/critical thresholds (80%, 95%, 100%).
/// Tracks per-service threshold states to avoid repeating alerts on every polling interval.
/// </summary>
public class NotificationService
{
    private readonly TrayIconManager _trayIconManager;
    private readonly SettingsService _settingsService;

    public NotificationService(TrayIconManager trayIconManager, SettingsService settingsService)
    {
        _trayIconManager = trayIconManager;
        _settingsService = settingsService;
    }

    public void CheckAndNotify(string serviceKey, UsageResult result)
    {
        if (!result.Success || result.NotImplemented) return;

        var settings = _settingsService.Current;
        if (!settings.Services.TryGetValue(serviceKey, out var serviceSettings) || !serviceSettings.Enabled)
            return;

        double percent = result.PercentUsed;
        int currentThreshold = percent switch
        {
            >= 100 => 100,
            >= 95 => 95,
            >= 80 => 80,
            _ => 0
        };

        // If usage dropped below previous threshold (e.g. quota reset), reset tracking
        if (currentThreshold < serviceSettings.LastNotifiedThreshold)
        {
            serviceSettings.LastNotifiedThreshold = currentThreshold;
            return;
        }

        // Check if we crossed a new higher threshold that hasn't been alerted yet
        if (currentThreshold > serviceSettings.LastNotifiedThreshold)
        {
            bool shouldNotify = currentThreshold switch
            {
                100 => serviceSettings.NotifyAt100,
                95 => serviceSettings.NotifyAt95,
                80 => serviceSettings.NotifyAt80,
                _ => false
            };

            if (shouldNotify)
            {
                serviceSettings.LastNotifiedThreshold = currentThreshold;
                SendThresholdAlert(serviceKey, percent, currentThreshold, result.ResetTimeText);
            }
        }
    }

    private void SendThresholdAlert(string serviceKey, double percent, int threshold, string? resetTimeText)
    {
        string resetInfo = !string.IsNullOrWhiteSpace(resetTimeText)
            ? $"\nReset: {resetTimeText}"
            : string.Empty;

        var (title, message, icon) = threshold switch
        {
            100 => (
                $"Orbit - {serviceKey} Quota Exhausted! (100%)",
                $"{serviceKey} subscription quota is fully used (100%).{resetInfo}",
                ToolTipIcon.Error
            ),
            95 => (
                $"Orbit - {serviceKey} Quota Critical! ({percent:0}%)",
                $"{serviceKey} usage has reached a critical {percent:0}%! You are near the limit.{resetInfo}",
                ToolTipIcon.Warning
            ),
            _ => (
                $"Orbit - {serviceKey} Quota Warning ({percent:0}%)",
                $"{serviceKey} usage reached {percent:0}%.{resetInfo}",
                ToolTipIcon.Info
            )
        };

        _trayIconManager.ShowNotification(title, message, icon);
    }
}
