using System.Windows;
using Orbit.Helpers;
using Orbit.Models;
using Orbit.Services;

namespace Orbit.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly SelectorConfigService _selectorService;
    private readonly WebView2SessionManager _session;
    private readonly UsageScraperService _scraper;
    private readonly MainWindow _mainWindow;
    private bool _suppressSliderSync;
    private bool _loaded;
    private bool _saved;

    // Snapshot taken on open, restored on Cancel so a live-previewed layout/monitor/offset/speed doesn't
    // stick around unsaved.
    private NotchLayout _initialLayout;
    private string? _initialTargetMonitorDeviceName;
    private double _initialOffsetX;
    private double _initialOffsetY;
    private NotchAnimationSpeed _initialAnimationSpeed;

    public SettingsWindow(
        SettingsService settingsService,
        SelectorConfigService selectorService,
        WebView2SessionManager session,
        UsageScraperService scraper,
        MainWindow mainWindow)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _selectorService = selectorService;
        _session = session;
        _scraper = scraper;
        _mainWindow = mainWindow;

        LoadFromSettings();
        Closing += SettingsWindow_Closing;
    }

    private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Covers closing via the title-bar X (Cancel/Save already handle their own paths) - an
        // unsaved live preview must not stick around.
        if (!_saved)
            _mainWindow.PreviewSettings(_initialLayout, _initialTargetMonitorDeviceName, _initialOffsetX, _initialOffsetY, _initialAnimationSpeed);
    }

    private void LoadFromSettings()
    {
        var settings = _settingsService.Current;
        var claude = settings.Services.GetValueOrDefault("Claude") ?? new ServiceSettings { Enabled = true };

        ClaudeEnabledCheck.IsChecked = claude.Enabled;
        ClaudeManualModeCheck.IsChecked = claude.ManualMode;
        ClaudeManualPercentBox.Text = claude.ManualPercent.ToString("0");
        _suppressSliderSync = true;
        ClaudeManualPercentSlider.Value = claude.ManualPercent;
        _suppressSliderSync = false;
        ClaudeNotify80Check.IsChecked = claude.NotifyAt80;
        ClaudeNotify95Check.IsChecked = claude.NotifyAt95;
        ClaudeNotify100Check.IsChecked = claude.NotifyAt100;

        var antigravity = settings.Services.GetValueOrDefault("Antigravity") ?? new ServiceSettings { Enabled = true, ManualMode = true };

        AntigravityEnabledCheck.IsChecked = antigravity.Enabled;
        AntigravityManualModeCheck.IsChecked = antigravity.ManualMode;
        AntigravityManualPercentBox.Text = antigravity.ManualPercent.ToString("0");
        _suppressSliderSync = true;
        AntigravityManualPercentSlider.Value = antigravity.ManualPercent;
        _suppressSliderSync = false;
        AntigravityNotify80Check.IsChecked = antigravity.NotifyAt80;
        AntigravityNotify95Check.IsChecked = antigravity.NotifyAt95;
        AntigravityNotify100Check.IsChecked = antigravity.NotifyAt100;

        IntervalBox.Text = settings.RefreshIntervalMinutes.ToString();

        var monitors = ScreenPositionHelper.GetMonitors();
        MonitorComboBox.ItemsSource = monitors;
        var selectedMonitor = monitors.FirstOrDefault(m => m.DeviceName == settings.TargetMonitorDeviceName)
                              ?? monitors.FirstOrDefault(m => m.IsPrimary)
                              ?? monitors.FirstOrDefault();
        if (selectedMonitor != null)
            MonitorComboBox.SelectedValue = selectedMonitor.DeviceName;

        OffsetXBox.Text = settings.NotchOffsetX.ToString("0");
        OffsetYBox.Text = settings.NotchOffsetY.ToString("0");
        _suppressSliderSync = true;
        OffsetXSlider.Value = settings.NotchOffsetX;
        OffsetYSlider.Value = settings.NotchOffsetY;
        _suppressSliderSync = false;

        if (settings.Layout == NotchLayout.RightCenter)
            LayoutRightCenterRadio.IsChecked = true;
        else
            LayoutTopCenterRadio.IsChecked = true;

        StartWithWindowsCheck.IsChecked = settings.StartWithWindows;

        (settings.AnimationSpeed switch
        {
            NotchAnimationSpeed.Fast => AnimSpeedFastRadio,
            NotchAnimationSpeed.Fluid => AnimSpeedFluidRadio,
            _ => AnimSpeedNormalRadio,
        }).IsChecked = true;

        _initialLayout = settings.Layout;
        _initialTargetMonitorDeviceName = settings.TargetMonitorDeviceName;
        _initialOffsetX = settings.NotchOffsetX;
        _initialOffsetY = settings.NotchOffsetY;
        _initialAnimationSpeed = settings.AnimationSpeed;
        _loaded = true;
    }

    /// <summary>Applies the currently-selected layout/monitor/offset/speed to the live notch window
    /// immediately, without saving - lets the user see the result before committing it.</summary>
    private void PreviewCurrent()
    {
        if (!_loaded) return;

        var layout = LayoutRightCenterRadio.IsChecked == true ? NotchLayout.RightCenter : NotchLayout.TopCenter;
        var monitorDeviceName = MonitorComboBox.SelectedValue as string;
        double offsetX = double.TryParse(OffsetXBox.Text, out var x) ? Math.Clamp(x, -100, 100) : 0;
        double offsetY = double.TryParse(OffsetYBox.Text, out var y) ? Math.Clamp(y, -100, 100) : 0;
        var speed = AnimSpeedFastRadio.IsChecked == true ? NotchAnimationSpeed.Fast
            : AnimSpeedFluidRadio.IsChecked == true ? NotchAnimationSpeed.Fluid
            : NotchAnimationSpeed.Normal;
        _mainWindow.PreviewSettings(layout, monitorDeviceName, offsetX, offsetY, speed);
    }

    private void ClaudeManualPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderSync) return;
        ClaudeManualPercentBox.Text = e.NewValue.ToString("0");
    }

    private void AntigravityManualPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderSync) return;
        AntigravityManualPercentBox.Text = e.NewValue.ToString("0");
    }

    private void OffsetXSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderSync) return;
        OffsetXBox.Text = e.NewValue.ToString("0"); // triggers OffsetXBox_TextChanged, which previews
    }

    private void OffsetYSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderSync) return;
        OffsetYBox.Text = e.NewValue.ToString("0"); // triggers OffsetYBox_TextChanged, which previews
    }

    private void OffsetXBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressSliderSync) return;
        if (double.TryParse(OffsetXBox.Text, out var value))
        {
            _suppressSliderSync = true;
            OffsetXSlider.Value = Math.Clamp(value, OffsetXSlider.Minimum, OffsetXSlider.Maximum);
            _suppressSliderSync = false;
        }
        PreviewCurrent();
    }

    private void OffsetYBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressSliderSync) return;
        if (double.TryParse(OffsetYBox.Text, out var value))
        {
            _suppressSliderSync = true;
            OffsetYSlider.Value = Math.Clamp(value, OffsetYSlider.Minimum, OffsetYSlider.Maximum);
            _suppressSliderSync = false;
        }
        PreviewCurrent();
    }

    private void MonitorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => PreviewCurrent();

    private void LayoutRadio_Checked(object sender, RoutedEventArgs e) => PreviewCurrent();

    private void AnimSpeedRadio_Checked(object sender, RoutedEventArgs e) => PreviewCurrent();

    private async void ClaudeLoginButton_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Opening sign-in window...";
        try
        {
            var selectors = _selectorService.Current;
            var url = selectors.Services.TryGetValue("Claude", out var cfg) && !string.IsNullOrWhiteSpace(cfg.UsageUrl)
                ? cfg.UsageUrl
                : "https://claude.ai";
            await _session.ShowLoginWindowAsync(url, this);
            StatusText.Text = "Sign-in window closed. Session saved locally.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not open sign-in window: {ex.Message}";
        }
    }

    /// <summary>
    /// Probes Antigravity's Chrome DevTools Protocol port and lists whatever windows/webviews it
    /// finds - this is the diagnostic tool for figuring out targetUrlContains/usageTextSelector in
    /// selectors.json (there's no "log in via Orbit" flow for a desktop app like there is for
    /// Claude/ChatGPT).
    /// </summary>
    private async void AntigravityDetectButton_Click(object sender, RoutedEventArgs e)
    {
        var selectors = _selectorService.Current;
        int port = selectors.Services.TryGetValue("Antigravity", out var cfg) && cfg.RemoteDebuggingPort > 0
            ? cfg.RemoteDebuggingPort
            : 9222;

        StatusText.Text = $"Checking http://127.0.0.1:{port}/json ...";
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var json = await http.GetStringAsync($"http://127.0.0.1:{port}/json");
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var lines = doc.RootElement.EnumerateArray()
                .Take(8)
                .Select(t =>
                {
                    var type = t.TryGetProperty("type", out var tp) ? tp.GetString() : "?";
                    var title = t.TryGetProperty("title", out var ti) ? ti.GetString() : "";
                    var targetUrl = t.TryGetProperty("url", out var u) ? u.GetString() : "";
                    return $"[{type}] {title} - {targetUrl}";
                })
                .ToList();

            StatusText.Text = lines.Count == 0
                ? $"Antigravity detected on port {port}, but it reported no open targets."
                : $"Found {lines.Count} target(s) on port {port}:\n" + string.Join("\n", lines) +
                  "\n\nShare this with Claude to figure out targetUrlContains/usageTextSelector for selectors.json.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Antigravity not detected on port {port}. Launch it with --remote-debugging-port={port} and try again. ({ex.GetType().Name}: {ex.Message})";
        }
    }

    private void ReloadSelectorsButton_Click(object sender, RoutedEventArgs e)
    {
        _selectorService.Reload();
        StatusText.Text = $"Reloaded selectors.json ({_selectorService.SelectorsPath}).";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Current;

        if (!settings.Services.TryGetValue("Claude", out var claude))
        {
            claude = new ServiceSettings();
            settings.Services["Claude"] = claude;
        }
        claude.Enabled = ClaudeEnabledCheck.IsChecked == true;
        claude.ManualMode = ClaudeManualModeCheck.IsChecked == true;
        if (double.TryParse(ClaudeManualPercentBox.Text, out var manualPercent))
            claude.ManualPercent = Math.Clamp(manualPercent, 0, 100);
        claude.NotifyAt80 = ClaudeNotify80Check.IsChecked == true;
        claude.NotifyAt95 = ClaudeNotify95Check.IsChecked == true;
        claude.NotifyAt100 = ClaudeNotify100Check.IsChecked == true;

        if (!settings.Services.TryGetValue("Antigravity", out var antigravity))
        {
            antigravity = new ServiceSettings();
            settings.Services["Antigravity"] = antigravity;
        }
        antigravity.Enabled = AntigravityEnabledCheck.IsChecked == true;
        antigravity.ManualMode = AntigravityManualModeCheck.IsChecked == true;
        if (double.TryParse(AntigravityManualPercentBox.Text, out var antigravityManualPercent))
            antigravity.ManualPercent = Math.Clamp(antigravityManualPercent, 0, 100);
        antigravity.NotifyAt80 = AntigravityNotify80Check.IsChecked == true;
        antigravity.NotifyAt95 = AntigravityNotify95Check.IsChecked == true;
        antigravity.NotifyAt100 = AntigravityNotify100Check.IsChecked == true;

        if (int.TryParse(IntervalBox.Text, out var interval))
            settings.RefreshIntervalMinutes = Math.Clamp(interval, 5, 120);

        settings.TargetMonitorDeviceName = MonitorComboBox.SelectedValue as string;

        if (double.TryParse(OffsetXBox.Text, out var offsetX))
            settings.NotchOffsetX = Math.Clamp(offsetX, -100, 100);
        if (double.TryParse(OffsetYBox.Text, out var offsetY))
            settings.NotchOffsetY = Math.Clamp(offsetY, -100, 100);

        settings.Layout = LayoutRightCenterRadio.IsChecked == true ? NotchLayout.RightCenter : NotchLayout.TopCenter;

        settings.AnimationSpeed = AnimSpeedFastRadio.IsChecked == true ? NotchAnimationSpeed.Fast
            : AnimSpeedFluidRadio.IsChecked == true ? NotchAnimationSpeed.Fluid
            : NotchAnimationSpeed.Normal;

        settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;

        _settingsService.Save(settings);
        _saved = true;

        // Live-apply without requiring a restart.
        _mainWindow.ApplyLayout(settings.Layout, settings.TargetMonitorDeviceName);
        _scraper.ApplyIntervalChange();
        StartupRegistrationService.SetStartWithWindows(settings.StartWithWindows);

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close(); // SettingsWindow_Closing reverts the preview
}
