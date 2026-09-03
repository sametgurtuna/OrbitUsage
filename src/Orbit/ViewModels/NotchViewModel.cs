using System.Collections.ObjectModel;
using Orbit.Helpers;
using Orbit.Models;

namespace Orbit.ViewModels;

/// <summary>
/// Backing view model for MainWindow. The parameterless constructor seeds mock data (used for
/// early UI/animation work and as a design-time fallback); App.xaml.cs uses the real constructor
/// once UsageScraperService exists, wiring live updates via ApplyUsageUpdate.
/// </summary>
public class NotchViewModel : ViewModelBase
{
    private static readonly (string Key, string DisplayName, string AccentColorHex)[] KnownServices =
    {
        ("Claude", "Claude", "#FF5722"),
        ("Antigravity", "Antigravity", "#38BDF8"),
        ("ChatGPT", "ChatGPT", "#10B981"),
    };

    private bool _isExpanded;
    private bool _isRefreshing;
    private ServiceUsageViewModel? _hoveredService;

    public ObservableCollection<ServiceUsageViewModel> Services { get; } = new();

    public ServiceUsageViewModel? HoveredService
    {
        get => _hoveredService;
        set => SetField(ref _hoveredService, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetField(ref _isRefreshing, value);
    }

    /// <summary>Aggregate status used to color the collapsed mini-notch dot: worst status across enabled services.</summary>
    public UsageStatus AggregateStatus
    {
        get
        {
            if (Services.Count == 0) return UsageStatus.Unavailable;
            if (Services.Any(s => s.Status == UsageStatus.Critical)) return UsageStatus.Critical;
            if (Services.Any(s => s.Status == UsageStatus.Warning)) return UsageStatus.Warning;
            if (Services.All(s => s.Status is UsageStatus.Unavailable or UsageStatus.NotImplemented)) return UsageStatus.Unavailable;
            return UsageStatus.Normal;
        }
    }

    public RelayCommand RefreshCommand { get; }

    /// <summary>Fired when the refresh button is clicked; App.xaml.cs wires this to UsageScraperService.RefreshNowAsync.</summary>
    public event Action? RefreshRequested;

    /// <summary>Mock-data constructor - used before real settings/scraper wiring exists.</summary>
    public NotchViewModel()
    {
        RefreshCommand = new RelayCommand(OnRefreshRequested);
        SeedMockData();
        HookAggregateStatus();
    }

    /// <summary>Real constructor: builds rows from AppSettings so enabled/manual state and
    /// ChatGPT/Gemini's "not implemented" placeholder reflect actual configuration.</summary>
    public NotchViewModel(AppSettings settings)
    {
        RefreshCommand = new RelayCommand(OnRefreshRequested);
        SeedFromSettings(settings);
        HookAggregateStatus();
    }

    private void HookAggregateStatus()
    {
        foreach (var svc in Services)
            svc.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AggregateStatus));
    }

    private void OnRefreshRequested() => RefreshRequested?.Invoke();

    public event EventHandler? NotchLayoutSizeChanged;

    /// <summary>Applies a live UsageResult from UsageScraperService.UsageUpdated to the matching row.</summary>
    public void ApplyUsageUpdate(string serviceKey, UsageResult result)
    {
        var row = Services.FirstOrDefault(s => s.ServiceKey == serviceKey);
        if (row == null) return;

        if (result.NotImplemented)
        {
            row.SetOverrideStatus(UsageStatus.NotImplemented, result.ErrorMessage ?? "Not implemented");
        }
        else if (result.Success)
        {
            row.PercentUsed = result.PercentUsed;
            row.ResetTimeText = result.ResetTimeText;
            row.LastUpdatedUtc = result.Timestamp;
            row.StatusMessage = null; // let PercentUsed drive DisplayText/Status again
            row.SetOverrideStatus(ComputeStatus(result.PercentUsed), null);

            if (result.HasSessionData)
            {
                row.HasSessionGauge = true;
                row.SessionPercentUsed = result.SessionPercentUsed!.Value;
                row.SessionResetTimeText = result.SessionResetTimeText;
            }
        }
        else
        {
            // If we have a valid last-known percentage, retain it instead of showing "Data unavailable".
            if (row.PercentUsed == 0 && !row.LastUpdatedUtc.HasValue)
            {
                row.SetOverrideStatus(UsageStatus.Unavailable, "Data unavailable");
            }
        }
    }

    private static UsageStatus ComputeStatus(double percent) => percent switch
    {
        >= 95 => UsageStatus.Critical,
        >= 80 => UsageStatus.Warning,
        _ => UsageStatus.Normal
    };

    public void RebuildFromSettings(AppSettings settings)
    {
        Services.Clear();
        SeedFromSettings(settings);
        HookAggregateStatus();
        OnPropertyChanged(nameof(AggregateStatus));
        NotchLayoutSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SeedFromSettings(AppSettings settings)
    {
        foreach (var (key, displayName, accent) in KnownServices)
        {
            if (settings.Services.TryGetValue(key, out var serviceSettings) && !serviceSettings.Enabled)
                continue;

            var row = new ServiceUsageViewModel(key, displayName, accent);
            if (key is "Claude" or "Antigravity")
            {
                row.HasSessionGauge = true;
                row.SessionPercentUsed = serviceSettings?.ManualPercent ?? 0;
            }
            if (serviceSettings != null)
            {
                if (serviceSettings.ManualMode)
                {
                    row.PercentUsed = serviceSettings.ManualPercent;
                    row.SessionPercentUsed = serviceSettings.ManualPercent;
                    row.ResetTimeText = "Manual mode";
                    row.SessionResetTimeText = "Manual mode";
                    row.SetOverrideStatus(ComputeStatus(serviceSettings.ManualPercent), null);
                }
                else
                {
                    row.PercentUsed = serviceSettings.LastKnownPercent;
                    row.ResetTimeText = serviceSettings.LastKnownResetText;
                    row.LastUpdatedUtc = serviceSettings.LastUpdatedUtc == default ? null : serviceSettings.LastUpdatedUtc;
                    if (serviceSettings.LastError != null && row.PercentUsed == 0 && !row.LastUpdatedUtc.HasValue)
                        row.SetOverrideStatus(UsageStatus.Unavailable, "Data unavailable");

                    if (serviceSettings.LastKnownSessionPercent.HasValue)
                    {
                        row.HasSessionGauge = true;
                        row.SessionPercentUsed = serviceSettings.LastKnownSessionPercent.Value;
                        row.SessionResetTimeText = serviceSettings.LastKnownSessionResetText;
                    }
                }
            }
            row.SessionExpandedChanged += (s, e) => NotchLayoutSizeChanged?.Invoke(this, EventArgs.Empty);
            Services.Add(row);
        }

        // ChatGPT is still a stub provider regardless of its Enabled flag.
        var chatGptRow = Services.FirstOrDefault(s => s.ServiceKey == "ChatGPT");
        chatGptRow?.SetOverrideStatus(UsageStatus.NotImplemented, "Not implemented yet");
    }

    private void SeedMockData()
    {
        var claude = new ServiceUsageViewModel("Claude", "Claude", "#FF5722")
        {
            PercentUsed = 7,
            ResetTimeText = "Thu 12:00 AM",
            HasSessionGauge = true,
            SessionPercentUsed = 73,
            SessionResetTimeText = "in 51 min",
            LastUpdatedUtc = DateTime.UtcNow
        };
        var antigravity = new ServiceUsageViewModel("Antigravity", "Antigravity", "#38BDF8")
        {
            PercentUsed = 21,
            ResetTimeText = "in 3d 12h",
            HasSessionGauge = true,
            SessionPercentUsed = 52,
            SessionResetTimeText = "in 2h 15m",
            LastUpdatedUtc = DateTime.UtcNow
        };
        var chatGpt = new ServiceUsageViewModel("ChatGPT", "ChatGPT", "#10B981")
        {
            PercentUsed = 21,
            ResetTimeText = "in 4d",
            LastUpdatedUtc = DateTime.UtcNow
        };
        chatGpt.SetOverrideStatus(UsageStatus.NotImplemented, "Not implemented yet");

        Services.Add(claude);
        Services.Add(antigravity);
        Services.Add(chatGpt);
    }
}
