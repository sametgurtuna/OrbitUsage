using System.Windows.Input;
using Orbit.Helpers;
using Orbit.Models;
using Orbit.Services;
using Orbit.Views;
using Serilog;

namespace Orbit.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly SelectorConfigService? _selectorService;
    private readonly WebView2SessionManager? _session;
    private readonly UsageScraperService? _scraper;
    private readonly MainWindow? _mainWindow;
    private readonly NotchViewModel? _viewModel;
    private readonly LocalApiService? _localApi;

    private readonly NotchLayout _initialLayout;
    private readonly string? _initialTargetMonitorDeviceName;
    private readonly double _initialOffsetX;
    private readonly double _initialOffsetY;
    private readonly NotchAnimationSpeed _initialAnimationSpeed;
    private readonly NotchTheme _initialTheme;

    private bool _isInitialized;
    private bool _isSaved;

    // Services - Claude
    private bool _claudeEnabled;
    private bool _claudeManualMode;
    private double _claudeManualPercent;
    private bool _claudeNotify80;
    private bool _claudeNotify95;
    private bool _claudeNotify100;
    private bool _claudeNotifyReset;

    // Services - Antigravity
    private bool _antigravityEnabled;
    private bool _antigravityManualMode;
    private double _antigravityManualPercent;
    private bool _antigravityNotify80;
    private bool _antigravityNotify95;
    private bool _antigravityNotify100;
    private bool _antigravityNotifyReset;

    // Appearance
    private NotchLayout _layout;
    private NotchTheme _theme;
    private NotchAnimationSpeed _animationSpeed;
    private double _notchOffsetX;
    private double _notchOffsetY;
    private List<MonitorInfo> _monitors = new();
    private string? _selectedMonitorDeviceName;

    // System
    private int _refreshIntervalMinutes = 15;
    private bool _startWithWindows;
    private bool _alwaysOnTop;
    private bool _playNotificationSound = true;
    private bool _hotkeyEnabled;
    private string _hotkeyModifiers = "Win+Alt";
    private string _hotkeyKey = "O";
    private string _hotkeyPreviewText = "Win+Alt + O";
    private bool _enableLocalApi;
    private int _localApiPort = 18923;

    // UI Feedback
    private string _statusText = string.Empty;

    public event EventHandler? RequestClose;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClaudeLoginCommand { get; }
    public ICommand ReloadSelectorsCommand { get; }
    public ICommand OpenApiInBrowserCommand { get; }
    public ICommand TestSoundCommand { get; }

    public SettingsViewModel(
        SettingsService settingsService,
        SelectorConfigService? selectorService = null,
        WebView2SessionManager? session = null,
        UsageScraperService? scraper = null,
        MainWindow? mainWindow = null,
        NotchViewModel? viewModel = null,
        LocalApiService? localApi = null)
    {
        _settingsService = settingsService;
        _selectorService = selectorService;
        _session = session;
        _scraper = scraper;
        _mainWindow = mainWindow;
        _viewModel = viewModel;
        _localApi = localApi;

        var s = _settingsService.Current;
        _initialLayout = s.Layout;
        _initialTargetMonitorDeviceName = s.TargetMonitorDeviceName;
        _initialOffsetX = s.NotchOffsetX;
        _initialOffsetY = s.NotchOffsetY;
        _initialAnimationSpeed = s.AnimationSpeed;
        _initialTheme = s.Theme;

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        ClaudeLoginCommand = new RelayCommand(async () => await ExecuteClaudeLoginAsync());
        ReloadSelectorsCommand = new RelayCommand(ExecuteReloadSelectors);
        OpenApiInBrowserCommand = new RelayCommand(ExecuteOpenApiInBrowser);
        TestSoundCommand = new RelayCommand(ExecuteTestSound);

        LoadSettings();
    }

    private void ExecuteTestSound()
    {
        NotificationService.PlaySound();
        StatusText = "Played test notification chime";
    }

    #region Properties - Claude

    public bool ClaudeEnabled
    {
        get => _claudeEnabled;
        set => SetField(ref _claudeEnabled, value);
    }

    public bool ClaudeManualMode
    {
        get => _claudeManualMode;
        set => SetField(ref _claudeManualMode, value);
    }

    public double ClaudeManualPercent
    {
        get => _claudeManualPercent;
        set => SetField(ref _claudeManualPercent, Math.Clamp(value, 0, 100));
    }

    public bool ClaudeNotify80
    {
        get => _claudeNotify80;
        set => SetField(ref _claudeNotify80, value);
    }

    public bool ClaudeNotify95
    {
        get => _claudeNotify95;
        set => SetField(ref _claudeNotify95, value);
    }

    public bool ClaudeNotify100
    {
        get => _claudeNotify100;
        set => SetField(ref _claudeNotify100, value);
    }

    public bool ClaudeNotifyReset
    {
        get => _claudeNotifyReset;
        set => SetField(ref _claudeNotifyReset, value);
    }

    #endregion

    #region Properties - Antigravity

    public bool AntigravityEnabled
    {
        get => _antigravityEnabled;
        set => SetField(ref _antigravityEnabled, value);
    }

    public bool AntigravityManualMode
    {
        get => _antigravityManualMode;
        set => SetField(ref _antigravityManualMode, value);
    }

    public double AntigravityManualPercent
    {
        get => _antigravityManualPercent;
        set => SetField(ref _antigravityManualPercent, Math.Clamp(value, 0, 100));
    }

    public bool AntigravityNotify80
    {
        get => _antigravityNotify80;
        set => SetField(ref _antigravityNotify80, value);
    }

    public bool AntigravityNotify95
    {
        get => _antigravityNotify95;
        set => SetField(ref _antigravityNotify95, value);
    }

    public bool AntigravityNotify100
    {
        get => _antigravityNotify100;
        set => SetField(ref _antigravityNotify100, value);
    }

    public bool AntigravityNotifyReset
    {
        get => _antigravityNotifyReset;
        set => SetField(ref _antigravityNotifyReset, value);
    }

    #endregion

    #region Properties - Appearance & Layout

    public NotchLayout Layout
    {
        get => _layout;
        set
        {
            if (SetField(ref _layout, value))
            {
                OnPropertyChanged(nameof(IsLayoutRightCenter));
                OnPropertyChanged(nameof(IsLayoutTopCenter));
                TriggerPreview();
            }
        }
    }

    public bool IsLayoutRightCenter
    {
        get => _layout == NotchLayout.RightCenter;
        set { if (value) Layout = NotchLayout.RightCenter; }
    }

    public bool IsLayoutTopCenter
    {
        get => _layout == NotchLayout.TopCenter;
        set { if (value) Layout = NotchLayout.TopCenter; }
    }

    public NotchTheme Theme
    {
        get => _theme;
        set
        {
            if (SetField(ref _theme, value))
            {
                OnPropertyChanged(nameof(IsThemeLight));
                OnPropertyChanged(nameof(IsThemeDark));
                OnPropertyChanged(nameof(IsThemeSystem));
                if (_isInitialized) ThemeManager.ApplyTheme(value);
            }
        }
    }

    public bool IsThemeLight
    {
        get => _theme == NotchTheme.Light;
        set { if (value) Theme = NotchTheme.Light; }
    }

    public bool IsThemeDark
    {
        get => _theme == NotchTheme.Dark;
        set { if (value) Theme = NotchTheme.Dark; }
    }

    public bool IsThemeSystem
    {
        get => _theme == NotchTheme.System;
        set { if (value) Theme = NotchTheme.System; }
    }

    public NotchAnimationSpeed AnimationSpeed
    {
        get => _animationSpeed;
        set
        {
            if (SetField(ref _animationSpeed, value))
            {
                OnPropertyChanged(nameof(IsSpeedNormal));
                OnPropertyChanged(nameof(IsSpeedFast));
                OnPropertyChanged(nameof(IsSpeedFluid));
                TriggerPreview();
            }
        }
    }

    public bool IsSpeedNormal
    {
        get => _animationSpeed == NotchAnimationSpeed.Normal;
        set { if (value) AnimationSpeed = NotchAnimationSpeed.Normal; }
    }

    public bool IsSpeedFast
    {
        get => _animationSpeed == NotchAnimationSpeed.Fast;
        set { if (value) AnimationSpeed = NotchAnimationSpeed.Fast; }
    }

    public bool IsSpeedFluid
    {
        get => _animationSpeed == NotchAnimationSpeed.Fluid;
        set { if (value) AnimationSpeed = NotchAnimationSpeed.Fluid; }
    }

    public double NotchOffsetX
    {
        get => _notchOffsetX;
        set
        {
            if (SetField(ref _notchOffsetX, Math.Clamp(value, -100, 100)))
            {
                TriggerPreview();
            }
        }
    }

    public double NotchOffsetY
    {
        get => _notchOffsetY;
        set
        {
            if (SetField(ref _notchOffsetY, Math.Clamp(value, -100, 100)))
            {
                TriggerPreview();
            }
        }
    }

    public List<MonitorInfo> Monitors
    {
        get => _monitors;
        set => SetField(ref _monitors, value);
    }

    public string? SelectedMonitorDeviceName
    {
        get => _selectedMonitorDeviceName;
        set
        {
            if (SetField(ref _selectedMonitorDeviceName, value))
            {
                TriggerPreview();
            }
        }
    }

    #endregion

    #region Properties - System & Shortcuts

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set => SetField(ref _refreshIntervalMinutes, Math.Clamp(value, 5, 120));
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetField(ref _startWithWindows, value);
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetField(ref _alwaysOnTop, value);
    }

    public bool PlayNotificationSound
    {
        get => _playNotificationSound;
        set => SetField(ref _playNotificationSound, value);
    }

    public bool HotkeyEnabled
    {
        get => _hotkeyEnabled;
        set => SetField(ref _hotkeyEnabled, value);
    }

    public string[] AvailableModifiers { get; } = { "Win+Alt", "Ctrl+Alt", "Ctrl+Shift", "Alt+Shift" };

    public string HotkeyModifiers
    {
        get => _hotkeyModifiers;
        set
        {
            if (SetField(ref _hotkeyModifiers, value))
            {
                UpdateHotkeyPreview();
            }
        }
    }

    public string HotkeyKey
    {
        get => _hotkeyKey;
        set
        {
            string sanitized = string.IsNullOrWhiteSpace(value) ? "O" : value.Trim().ToUpperInvariant();
            if (SetField(ref _hotkeyKey, sanitized))
            {
                UpdateHotkeyPreview();
            }
        }
    }

    public string HotkeyPreviewText
    {
        get => _hotkeyPreviewText;
        private set => SetField(ref _hotkeyPreviewText, value);
    }

    public bool EnableLocalApi
    {
        get => _enableLocalApi;
        set => SetField(ref _enableLocalApi, value);
    }

    public int LocalApiPort
    {
        get => _localApiPort;
        set => SetField(ref _localApiPort, Math.Clamp(value, 1024, 65535));
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    #endregion

    private void LoadSettings()
    {
        var settings = _settingsService.Current;

        var claude = settings.Services.GetValueOrDefault("Claude") ?? new ServiceSettings { Enabled = true };
        _claudeEnabled = claude.Enabled;
        _claudeManualMode = claude.ManualMode;
        _claudeManualPercent = claude.ManualPercent;
        _claudeNotify80 = claude.NotifyAt80;
        _claudeNotify95 = claude.NotifyAt95;
        _claudeNotify100 = claude.NotifyAt100;
        _claudeNotifyReset = claude.NotifyOnReset;

        var antigravity = settings.Services.GetValueOrDefault("Antigravity") ?? new ServiceSettings { Enabled = true, ManualMode = true };
        _antigravityEnabled = antigravity.Enabled;
        _antigravityManualMode = antigravity.ManualMode;
        _antigravityManualPercent = antigravity.ManualPercent;
        _antigravityNotify80 = antigravity.NotifyAt80;
        _antigravityNotify95 = antigravity.NotifyAt95;
        _antigravityNotify100 = antigravity.NotifyAt100;
        _antigravityNotifyReset = antigravity.NotifyOnReset;

        _refreshIntervalMinutes = settings.RefreshIntervalMinutes;
        _monitors = ScreenPositionHelper.GetMonitors();
        _selectedMonitorDeviceName = _monitors.FirstOrDefault(m => m.DeviceName == settings.TargetMonitorDeviceName)?.DeviceName
                                     ?? _monitors.FirstOrDefault(m => m.IsPrimary)?.DeviceName
                                     ?? _monitors.FirstOrDefault()?.DeviceName;

        _notchOffsetX = settings.NotchOffsetX;
        _notchOffsetY = settings.NotchOffsetY;
        _layout = settings.Layout;
        _theme = settings.Theme;
        _animationSpeed = settings.AnimationSpeed;

        _startWithWindows = settings.StartWithWindows;
        _alwaysOnTop = settings.AlwaysOnTop;
        _playNotificationSound = settings.PlayNotificationSound;
        _hotkeyEnabled = settings.HotkeyEnabled;
        _hotkeyModifiers = settings.HotkeyModifiers ?? "Win+Alt";
        _hotkeyKey = settings.HotkeyKey ?? "O";
        UpdateHotkeyPreview();

        _enableLocalApi = settings.EnableLocalApi;
        _localApiPort = settings.LocalApiPort;

        _isInitialized = true;
    }

    private void UpdateHotkeyPreview()
    {
        HotkeyPreviewText = $"{HotkeyModifiers} + {HotkeyKey}";
    }

    private void TriggerPreview()
    {
        if (!_isInitialized) return;
        _mainWindow?.PreviewSettings(Layout, SelectedMonitorDeviceName, NotchOffsetX, NotchOffsetY, AnimationSpeed);
    }

    public void OnWindowClosing()
    {
        if (!_isSaved)
        {
            RevertPreview();
        }
    }

    public void RevertPreview()
    {
        _mainWindow?.PreviewSettings(_initialLayout, _initialTargetMonitorDeviceName, _initialOffsetX, _initialOffsetY, _initialAnimationSpeed);
        ThemeManager.ApplyTheme(_initialTheme);
    }

    private void Save()
    {
        var settings = _settingsService.Current;

        if (!settings.Services.TryGetValue("Claude", out var claude))
        {
            claude = new ServiceSettings();
            settings.Services["Claude"] = claude;
        }
        claude.Enabled = ClaudeEnabled;
        claude.ManualMode = ClaudeManualMode;
        claude.ManualPercent = Math.Clamp(ClaudeManualPercent, 0, 100);
        claude.NotifyAt80 = ClaudeNotify80;
        claude.NotifyAt95 = ClaudeNotify95;
        claude.NotifyAt100 = ClaudeNotify100;
        claude.NotifyOnReset = ClaudeNotifyReset;

        if (!settings.Services.TryGetValue("Antigravity", out var antigravity))
        {
            antigravity = new ServiceSettings();
            settings.Services["Antigravity"] = antigravity;
        }
        antigravity.Enabled = AntigravityEnabled;
        antigravity.ManualMode = AntigravityManualMode;
        antigravity.ManualPercent = Math.Clamp(AntigravityManualPercent, 0, 100);
        antigravity.NotifyAt80 = AntigravityNotify80;
        antigravity.NotifyAt95 = AntigravityNotify95;
        antigravity.NotifyAt100 = AntigravityNotify100;
        antigravity.NotifyOnReset = AntigravityNotifyReset;

        settings.RefreshIntervalMinutes = Math.Clamp(RefreshIntervalMinutes, 5, 120);
        settings.TargetMonitorDeviceName = SelectedMonitorDeviceName;
        settings.NotchOffsetX = Math.Clamp(NotchOffsetX, -100, 100);
        settings.NotchOffsetY = Math.Clamp(NotchOffsetY, -100, 100);
        settings.Layout = Layout;
        settings.Theme = Theme;
        settings.AnimationSpeed = AnimationSpeed;

        settings.EnableLocalApi = EnableLocalApi;
        settings.LocalApiPort = Math.Clamp(LocalApiPort, 1024, 65535);

        settings.StartWithWindows = StartWithWindows;
        settings.AlwaysOnTop = AlwaysOnTop;
        settings.PlayNotificationSound = PlayNotificationSound;
        settings.HotkeyEnabled = HotkeyEnabled;
        settings.HotkeyModifiers = HotkeyModifiers;
        settings.HotkeyKey = HotkeyKey;

        _settingsService.Save(settings);
        _isSaved = true;

        Log.Information("[SettingsViewModel] Settings saved successfully");

        ThemeManager.ApplyTheme(settings.Theme);
        _mainWindow?.ApplyLayout(settings.Layout, settings.TargetMonitorDeviceName);
        _mainWindow?.RefreshHotkeysAndTopmost();
        _scraper?.ApplyIntervalChange();
        _viewModel?.RebuildFromSettings(settings);

        if (_localApi != null)
        {
            _ = _localApi.RestartAsync(settings.EnableLocalApi, settings.LocalApiPort);
        }

        StartupRegistrationService.SetStartWithWindows(settings.StartWithWindows);

        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        RevertPreview();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async Task ExecuteClaudeLoginAsync()
    {
        if (_session == null) return;
        StatusText = "Opening sign-in window...";
        try
        {
            var selectors = _selectorService?.Current;
            var url = selectors != null && selectors.Services.TryGetValue("Claude", out var cfg) && !string.IsNullOrWhiteSpace(cfg.UsageUrl)
                ? cfg.UsageUrl
                : "https://claude.ai";
            await _session.ShowLoginWindowAsync(url);
            StatusText = "Sign-in window closed. Session saved locally.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SettingsViewModel] Failed to open Claude login window");
            StatusText = $"Could not open sign-in window: {ex.Message}";
        }
    }

    private void ExecuteReloadSelectors()
    {
        if (_selectorService == null) return;
        _selectorService.Reload();
        StatusText = $"Reloaded selectors.json ({_selectorService.SelectorsPath}).";
        Log.Information("[SettingsViewModel] Reloaded selectors configuration from {Path}", _selectorService.SelectorsPath);
    }

    private void ExecuteOpenApiInBrowser()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://127.0.0.1:{LocalApiPort}/",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SettingsViewModel] Failed to launch local API in browser");
            StatusText = $"Could not open browser: {ex.Message}";
        }
    }
}
