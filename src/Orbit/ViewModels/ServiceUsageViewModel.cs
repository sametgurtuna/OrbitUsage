using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace Orbit.ViewModels;

public enum QuotaWindowMode
{
    FiveHour,
    Weekly
}

/// <summary>One row in the expanded notch panel - one LLM service's usage state.</summary>
public class ServiceUsageViewModel : ViewModelBase
{
    private double _percentUsed;
    private UsageStatus _status = UsageStatus.Normal;
    private DateTime? _lastUpdatedUtc;
    private string? _statusMessage;
    private string? _resetTimeText;

    private bool _hasSessionGauge;
    private bool _isSessionExpanded;
    private double _sessionPercentUsed;
    private string? _sessionResetTimeText;
    private QuotaWindowMode _activeQuotaMode = QuotaWindowMode.Weekly;

    public string ServiceKey { get; }
    public string DisplayName { get; }

    public QuotaWindowMode ActiveQuotaMode
    {
        get => _activeQuotaMode;
        set
        {
            if (SetField(ref _activeQuotaMode, value))
                NotifyActiveQuotaChanged();
        }
    }

    /// <summary>Brand-adjacent accent color for this service's dot/icon (not the actual logo).</summary>
    public string AccentColorHex { get; }

    public event EventHandler? SessionExpandedChanged;

    public bool HasSessionGauge
    {
        get => _hasSessionGauge;
        set
        {
            if (SetField(ref _hasSessionGauge, value))
            {
                OnPropertyChanged(nameof(SessionVisibility));
                NotifyActiveQuotaChanged();
            }
        }
    }

    public bool IsSessionExpanded
    {
        get => _isSessionExpanded;
        set
        {
            if (SetField(ref _isSessionExpanded, value))
            {
                OnPropertyChanged(nameof(SessionVisibility));
                SessionExpandedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public System.Windows.Visibility SessionVisibility => _isSessionExpanded && _hasSessionGauge
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;

    public double SessionPercentUsed
    {
        get => _sessionPercentUsed;
        set
        {
            if (SetField(ref _sessionPercentUsed, value))
            {
                OnPropertyChanged(nameof(SessionDisplayText));
                OnPropertyChanged(nameof(SessionGaugeText));
                NotifyActiveQuotaChanged();
            }
        }
    }

    public string? SessionResetTimeText
    {
        get => _sessionResetTimeText;
        set
        {
            if (SetField(ref _sessionResetTimeText, value))
            {
                OnPropertyChanged(nameof(SessionResetDisplay));
                OnPropertyChanged(nameof(SessionResetVisibility));
                NotifyActiveQuotaChanged();
            }
        }
    }

    public string SessionDisplayText => $"{SessionPercentUsed:0}%";
    public string SessionGaugeText => $"{SessionPercentUsed:0}%";
    public string SessionLabel => "5h Limit";

    public string SessionResetDisplay => !string.IsNullOrWhiteSpace(SessionResetTimeText)
        ? (SessionResetTimeText.StartsWith("⏳") ? SessionResetTimeText : $"⏳ {SessionResetTimeText}")
        : string.Empty;

    public System.Windows.Visibility SessionResetVisibility => !string.IsNullOrWhiteSpace(SessionResetTimeText)
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;

    public void ToggleSessionExpanded()
    {
        if (HasSessionGauge)
        {
            IsSessionExpanded = !IsSessionExpanded;
        }
    }

    public void ToggleQuotaMode()
    {
        if (HasSessionGauge)
        {
            ActiveQuotaMode = ActiveQuotaMode == QuotaWindowMode.FiveHour
                ? QuotaWindowMode.Weekly
                : QuotaWindowMode.FiveHour;
        }
    }

    public double ActivePercentUsed => (ActiveQuotaMode == QuotaWindowMode.FiveHour && HasSessionGauge)
        ? SessionPercentUsed
        : PercentUsed;

    public double ActivePercentRemaining => Math.Clamp(100.0 - ActivePercentUsed, 0, 100);

    public string ActivePercentDisplay => $"{ActivePercentRemaining:0}%";

    public string ActiveSubText => "left";

    public string ActiveWindowDisplay => (ActiveQuotaMode == QuotaWindowMode.FiveHour && HasSessionGauge)
        ? "5h"
        : "Weekly";

    public string? ActiveResetDisplay => (ActiveQuotaMode == QuotaWindowMode.FiveHour && HasSessionGauge)
        ? SessionResetTimeText
        : ResetTimeText;

    public string ActiveStatusDisplay
    {
        get
        {
            if (Status == UsageStatus.NotImplemented)
                return "Not supported";
            if (Status == UsageStatus.Unavailable && ActivePercentUsed <= 0)
                return "Unavailable";

            double rem = ActivePercentRemaining;
            if (rem >= 60)
                return "Plenty";
            if (rem >= 30)
                return "On pace";

            if (!string.IsNullOrWhiteSpace(ActiveResetDisplay))
                return $"Reset {ActiveResetDisplay}";

            return "Low";
        }
    }

    public Brush ActiveStatusBrush
    {
        get
        {
            if (Status == UsageStatus.Unavailable || Status == UsageStatus.NotImplemented)
                return new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

            double rem = ActivePercentRemaining;
            if (rem >= 60)
                return new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // Apple Green
            if (rem >= 30)
                return new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)); // Apple Amber/Yellow
            return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));    // Apple Coral/Red
        }
    }

    public string TopLimitTitle => ServiceKey switch
    {
        "Claude" => "Current session",
        "Antigravity" => "5 hour limit",
        _ => "Current session"
    };

    public double TopLimitPercent
    {
        get => HasSessionGauge ? SessionPercentUsed : PercentUsed;
        set { }
    }

    public string TopLimitPercentDisplay => $"{TopLimitPercent:0}%";
    public string TopLimitUsedDisplay => $"{TopLimitPercent:0}% Used";

    public string TopLimitResetDisplay
    {
        get
        {
            var text = HasSessionGauge
                ? SessionResetTimeText
                : ResetTimeText;

            if (string.IsNullOrWhiteSpace(text))
                return ServiceKey is "Claude" or "Antigravity" ? "Resets in 5h" : string.Empty;

            text = text.Replace("⏳", "").Trim();
            if (text.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
                return $"Resets {text}";
            if (text.StartsWith("Resets", StringComparison.OrdinalIgnoreCase))
                return text;
            return $"Resets in {text}";
        }
    }

    public Brush TopLimitBrush => ServiceKey switch
    {
        "Claude" => new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x36)),
        "Antigravity" => new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
        _ => AccentBrush
    };

    public string BottomLimitTitle => ServiceKey switch
    {
        "Claude" => "All models",
        _ => "Weekly limit"
    };

    public double BottomLimitPercent
    {
        get => PercentUsed;
        set { }
    }

    public string BottomLimitPercentDisplay => $"{BottomLimitPercent:0}%";
    public string BottomLimitUsedDisplay => $"{BottomLimitPercent:0}% Used";

    public string BottomLimitResetDisplay
    {
        get
        {
            var text = ResetTimeText;
            if (string.IsNullOrWhiteSpace(text))
                return "Resets weekly";

            text = text.Replace("⏳", "").Trim();
            if (text.StartsWith("Resets", StringComparison.OrdinalIgnoreCase))
                return text;
            if (text.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
                return $"Resets {text}";
            return $"Resets {text}";
        }
    }

    public Brush BottomLimitBrush => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

    public double DockPercentUsed => HasSessionGauge ? SessionPercentUsed : PercentUsed;

    public string DockPercentDisplay => $"{DockPercentUsed:0}%";

    public string RelativeTimeDisplay
    {
        get
        {
            if (!LastUpdatedUtc.HasValue)
                return "Just now";
            var span = DateTime.UtcNow - LastUpdatedUtc.Value;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }

    private static readonly ImageSource? s_claudeImage = LoadResourceImage("claude.png");
    private static readonly ImageSource? s_antigravityImage = LoadResourceImage("antigravity.png");

    private static ImageSource? LoadResourceImage(string filename)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Orbit;component/{filename}", UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            try
            {
                var fallbackUri = new Uri($"pack://application:,,,/{filename}", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = fallbackUri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }

    public ImageSource? ServiceIconImageSource => ServiceKey switch
    {
        "Claude" => s_claudeImage,
        "Antigravity" => s_antigravityImage,
        _ => null
    };

    public System.Windows.Visibility ImageLogoVisibility => ServiceIconImageSource != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility VectorLogoVisibility => ServiceIconImageSource == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public Geometry ServiceIconGeometry => ServiceKey switch
    {
        "Claude" => Geometry.Parse("M12,2 L13.5,7.5 L18.5,4.5 L16,9.5 L21.5,9.5 L17.5,13.5 L22,16 L16.8,16.5 L17.5,21.5 L13.5,17.8 L12,22.5 L10.5,17.8 L6.5,21.5 L7.2,16.5 L2,16 L6.5,13.5 L2.5,9.5 L8,9.5 L5.5,4.5 L10.5,7.5 Z"),
        "Antigravity" => Geometry.Parse("M12,3.5 L20.5,19 L16.5,19 L12,10.2 L7.5,19 L3.5,19 Z"),
        "ChatGPT" or "ChatGpt" or "Codex" => Geometry.Parse("M20.5,10.2 C20.1,7.8 18.2,5.9 15.8,5.5 C15.5,4.4 14.8,3.4 13.8,2.7 C11.8,1.3 9.1,1.7 7.6,3.6 C7.1,3.4 6.5,3.3 6,3.4 C3.6,3.8 1.9,5.8 1.9,8.2 C1.9,8.7 2,9.3 2.3,9.8 C1.3,11.2 0.9,13 1.3,14.7 C2,17.4 4.5,19.2 7.2,19.1 C7.5,20.2 8.2,21.2 9.2,21.9 C11.2,23.3 13.9,22.9 15.4,21 C15.9,21.2 16.5,21.3 17,21.2 C19.4,20.8 21.1,18.8 21.1,16.4 C21.1,15.9 21,15.3 20.7,14.8 C21.7,13.4 22.1,11.6 20.5,10.2 Z"),
        _ => Geometry.Parse("M12,2 A10,10 0 1,0 22,12 A10,10 0 0,0 12,2 Z")
    };

    public void NotifyActiveQuotaChanged()
    {
        OnPropertyChanged(nameof(ActiveQuotaMode));
        OnPropertyChanged(nameof(ActivePercentUsed));
        OnPropertyChanged(nameof(ActivePercentRemaining));
        OnPropertyChanged(nameof(ActivePercentDisplay));
        OnPropertyChanged(nameof(ActiveWindowDisplay));
        OnPropertyChanged(nameof(ActiveResetDisplay));
        OnPropertyChanged(nameof(ActiveStatusDisplay));
        OnPropertyChanged(nameof(ActiveStatusBrush));
        OnPropertyChanged(nameof(TopLimitTitle));
        OnPropertyChanged(nameof(TopLimitPercent));
        OnPropertyChanged(nameof(TopLimitPercentDisplay));
        OnPropertyChanged(nameof(TopLimitUsedDisplay));
        OnPropertyChanged(nameof(TopLimitResetDisplay));
        OnPropertyChanged(nameof(TopLimitBrush));
        OnPropertyChanged(nameof(BottomLimitTitle));
        OnPropertyChanged(nameof(BottomLimitPercent));
        OnPropertyChanged(nameof(BottomLimitPercentDisplay));
        OnPropertyChanged(nameof(BottomLimitUsedDisplay));
        OnPropertyChanged(nameof(BottomLimitResetDisplay));
        OnPropertyChanged(nameof(BottomLimitBrush));
        OnPropertyChanged(nameof(DockPercentUsed));
        OnPropertyChanged(nameof(DockPercentDisplay));
        OnPropertyChanged(nameof(RelativeTimeDisplay));
    }

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
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(FullToolTipText));
                NotifyActiveQuotaChanged();
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
                NotifyActiveQuotaChanged();
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
                sb.AppendLine($"Weekly Reset: {ResetTimeText}");
            if (HasSessionGauge)
                sb.AppendLine($"5h Limit: {SessionDisplayText} (Reset: {SessionResetTimeText ?? "soon"})");
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

    /// <summary>Status-driven color (green/amber/red/gray) used for the aggregate status dot.</summary>
    public Brush StatusBrush => Status switch
    {
        UsageStatus.Critical => new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B)),
        UsageStatus.Warning => new SolidColorBrush(Color.FromRgb(0xF2, 0xB8, 0x3D)),
        UsageStatus.Unavailable or UsageStatus.NotImplemented => new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)),
        _ => new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84)),
    };

    /// <summary>Brand-specific color brush for this service (Claude terracotta, ChatGPT off-white, Antigravity blue).</summary>
    public Brush AccentBrush
    {
        get
        {
            if (Status == UsageStatus.NotImplemented)
                return new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x70));

            if (Status == UsageStatus.Critical)
                return new SolidColorBrush(Color.FromRgb(0xE5, 0x4B, 0x4B));

            try
            {
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(AccentColorHex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            }
        }
    }

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
        OnPropertyChanged(nameof(AccentBrush));
    }

    private static UsageStatus ComputeStatus(double percent) => percent switch
    {
        >= 95 => UsageStatus.Critical,
        >= 80 => UsageStatus.Warning,
        _ => UsageStatus.Normal
    };
}
