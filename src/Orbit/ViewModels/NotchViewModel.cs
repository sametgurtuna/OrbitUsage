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
        ("Claude", "Claude", "#D97757"),
        ("ChatGPT", "ChatGPT", "#10A37F"),
        ("Antigravity", "Antigravity", "#4285F4"),
    };

    private bool _isExpanded;
    private bool _isRefreshing;

    public ObservableCollection<ServiceUsageViewModel> Services { get; } = new();

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
        }
        else
        {
            // Keep whatever PercentUsed currently shows (last known value) but flag it as stale.
            row.SetOverrideStatus(UsageStatus.Unavailable, "Data unavailable");
        }
    }

    private static UsageStatus ComputeStatus(double percent) => percent switch
    {
        >= 95 => UsageStatus.Critical,
        >= 80 => UsageStatus.Warning,
        _ => UsageStatus.Normal
    };

    private void SeedFromSettings(AppSettings settings)
    {
        foreach (var (key, displayName, accent) in KnownServices)
        {
            var row = new ServiceUsageViewModel(key, displayName, accent);
            if (settings.Services.TryGetValue(key, out var serviceSettings))
            {
                row.PercentUsed = serviceSettings.LastKnownPercent;
                row.ResetTimeText = serviceSettings.LastKnownResetText;
                row.LastUpdatedUtc = serviceSettings.LastUpdatedUtc == default ? null : serviceSettings.LastUpdatedUtc;
                if (serviceSettings.LastError != null)
                    row.SetOverrideStatus(UsageStatus.Unavailable, "Data unavailable");
            }
            Services.Add(row);
        }

        // ChatGPT is still a stub provider regardless of its Enabled flag.
        var chatGptRow = Services.FirstOrDefault(s => s.ServiceKey == "ChatGPT");
        chatGptRow?.SetOverrideStatus(UsageStatus.NotImplemented, "Not implemented yet");
    }

    private void SeedMockData()
    {
        var claude = new ServiceUsageViewModel("Claude", "Claude", "#D97757") { PercentUsed = 42, ResetTimeText = "in 2h 15m", LastUpdatedUtc = DateTime.UtcNow };
        var chatGpt = new ServiceUsageViewModel("ChatGPT", "ChatGPT", "#10A37F");
        chatGpt.SetOverrideStatus(UsageStatus.NotImplemented, "Not implemented yet");
        var antigravity = new ServiceUsageViewModel("Antigravity", "Antigravity", "#4285F4") { PercentUsed = 15, ResetTimeText = "in 3d", LastUpdatedUtc = DateTime.UtcNow };

        Services.Add(claude);
        Services.Add(chatGpt);
        Services.Add(antigravity);
    }
}
