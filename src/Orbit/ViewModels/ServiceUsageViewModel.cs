using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace Orbit.ViewModels;

/// <summary>One row in the expanded notch panel - one LLM service's usage state.</summary>
public class ServiceUsageViewModel : ViewModelBase
{
    private double _percentUsed;
    private UsageStatus _status = UsageStatus.Normal;
    private DateTime? _lastUpdatedUtc;
    private string? _statusMessage;

    private string? _resetTimeText;

    public string ServiceKey { get; }
    public string DisplayName { get; }

    /// <summary>Brand-adjacent accent color for this service's dot/icon (not the actual logo).</summary>
    public string AccentColorHex { get; }

    public double PercentUsed
    {
        get => _percentUsed;
        set
        {
            if (SetField(ref _percentUsed, value))
            {
                Status = ComputeStatus(value);
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(GaugeText));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(FullToolTipText));
            }
        }
    }

    public UsageStatus Status
    {
        get => _status;
        private set
        {
            if (SetField(ref _status, value))
                OnPropertyChanged(nameof(FullToolTipText));
        }
    }

    public DateTime? LastUpdatedUtc
    {
        get => _lastUpdatedUtc;
        set
        {
            if (SetField(ref _lastUpdatedUtc, value))
                OnPropertyChanged(nameof(FullToolTipText));
        }
    }

    public string? ResetTimeText
    {
        get => _resetTimeText;
        set
        {
            if (SetField(ref _resetTimeText, value))
            {
                OnPropertyChanged(nameof(ResetDisplay));
                OnPropertyChanged(nameof(FullToolTipText));
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(FullToolTipText));
            }
        }
    }

    /// <summary>Formatted short reset indicator for UI display.</summary>
    public string ResetDisplay => !string.IsNullOrWhiteSpace(ResetTimeText)
        ? (ResetTimeText.StartsWith("⏳") ? ResetTimeText : $"⏳ {ResetTimeText}")
        : string.Empty;

    public System.Windows.Visibility ResetVisibility => !string.IsNullOrWhiteSpace(ResetTimeText)
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;

    public string LastUpdatedDisplay => LastUpdatedUtc.HasValue
        ? $"Updated: {LastUpdatedUtc.Value.ToLocalTime():HH:mm:ss}"
        : "Not updated yet";

    /// <summary>Detailed tooltip text with usage, reset countdown, and last update time.</summary>
    public string FullToolTipText
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{DisplayName} - {DisplayText}");
            if (!string.IsNullOrWhiteSpace(ResetTimeText))
                sb.AppendLine($"Reset: {ResetTimeText}");
            if (LastUpdatedUtc.HasValue)
                sb.AppendLine($"Updated: {LastUpdatedUtc.Value.ToLocalTime():HH:mm:ss}");
            if (!string.IsNullOrWhiteSpace(StatusMessage))
                sb.AppendLine($"Status: {StatusMessage}");
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>What the row's trailing label shows: "42%" normally, or an override message (e.g. "Not implemented yet").</summary>
    public string DisplayText => Status switch
    {
        UsageStatus.NotImplemented => StatusMessage ?? "Not implemented",
        UsageStatus.Unavailable when StatusMessage != null => StatusMessage,
        _ => $"{PercentUsed:0}%"
    };

    /// <summary>Compact text for the radial gauge's center label: a short "--" placeholder when there's no real quota to show.</summary>
    public string GaugeText => Status switch
    {
        UsageStatus.NotImplemented => "--",
        UsageStatus.Unavailable when PercentUsed <= 0 => "--",
        _ => $"{PercentUsed:0}%"
    };

    /// <summary>True only when a real quota value should drive the gauge's arc (Normal/Warning/Critical, or stale-but-known Unavailable).</summary>
    public bool IsActive => Status is UsageStatus.Normal or UsageStatus.Warning or UsageStatus.Critical
        || (Status == UsageStatus.Unavailable && PercentUsed > 0);

    /// <summary>Status-driven color (green/amber/red/gray) used for the gauge arc, more legible at a glance than the brand accent.</summary>
    public Brush StatusBrush => Status switch
    {
        UsageStatus.Critical => new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B)),
        UsageStatus.Warning => new SolidColorBrush(Color.FromRgb(0xF2, 0xB8, 0x3D)),
        UsageStatus.Unavailable or UsageStatus.NotImplemented => new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)),
        _ => new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84)),
    };

    public ServiceUsageViewModel(string serviceKey, string displayName, string accentColorHex)
    {
        ServiceKey = serviceKey;
        DisplayName = displayName;
        AccentColorHex = accentColorHex;
    }

    /// <summary>Force a non-quota status (Unavailable/NotImplemented) without touching PercentUsed's own threshold logic.</summary>
    public void SetOverrideStatus(UsageStatus status, string? message)
    {
        _status = status;
        _statusMessage = message;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(GaugeText));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private static UsageStatus ComputeStatus(double percent) => percent switch
    {
        >= 95 => UsageStatus.Critical,
        >= 80 => UsageStatus.Warning,
        _ => UsageStatus.Normal
    };
}
