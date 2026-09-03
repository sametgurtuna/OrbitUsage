using System.Drawing;
using System.Windows.Forms;
using Orbit.Views;

namespace Orbit.Services;

/// <summary>
/// System tray icon + context menu (Show/Hide, Reload Now, Settings, Exit). Uses plain
/// System.Windows.Forms.NotifyIcon rather than a third-party WPF tray library to avoid an extra
/// dependency for what is just a context menu.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MainWindow _mainWindow;
    private readonly UsageScraperService _scraper;
    private readonly Func<SettingsWindow> _settingsWindowFactory;
    private SettingsWindow? _openSettingsWindow;

    public TrayIconManager(MainWindow mainWindow, UsageScraperService scraper, Func<SettingsWindow> settingsWindowFactory)
    {
        _mainWindow = mainWindow;
        _scraper = scraper;
        _settingsWindowFactory = settingsWindowFactory;

        var menu = new ContextMenuStrip();
        var showHideItem = new ToolStripMenuItem("Show/Hide notch");
        showHideItem.Click += (_, _) => ToggleVisibility();
        var reloadItem = new ToolStripMenuItem("Reload Now");
        reloadItem.Click += async (_, _) => await _scraper.RefreshNowAsync();
        var launchAntigravityItem = new ToolStripMenuItem("Launch Antigravity (Port 9222)");
        launchAntigravityItem.Click += (_, _) => Helpers.AntigravityLauncherHelper.Launch();
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettings();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(showHideItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(launchAntigravityItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "Orbit",
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            _mainWindow.Visibility = System.Windows.Visibility.Visible;
            _mainWindow.Activate();
            _mainWindow.Expand();
        };
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 5000)
    {
        _notifyIcon.ShowBalloonTip(timeoutMs, title, message, icon);
    }

    private void ToggleVisibility()
    {
        _mainWindow.Visibility = _mainWindow.Visibility == System.Windows.Visibility.Visible
            ? System.Windows.Visibility.Hidden
            : System.Windows.Visibility.Visible;
    }

    private void OpenSettings()
    {
        if (_openSettingsWindow != null)
        {
            _openSettingsWindow.Activate();
            return;
        }

        _openSettingsWindow = _settingsWindowFactory();
        _openSettingsWindow.Closed += (_, _) => _openSettingsWindow = null;
        _openSettingsWindow.Show();
        _openSettingsWindow.Activate();
    }

    /// <summary>Loads the high-resolution Orbit application icon for the Windows system tray.</summary>
    private static Icon CreateTrayIcon()
    {
        try
        {
            var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/orbit.ico", UriKind.Absolute));
            if (streamInfo?.Stream != null)
            {
                return new Icon(streamInfo.Stream, 32, 32);
            }
        }
        catch { }

        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
            {
                var ico = Icon.ExtractAssociatedIcon(exePath);
                if (ico != null) return ico;
            }
        }
        catch { }

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(255, 0x8B, 0x5C, 0xF6));
            g.FillEllipse(brush, 4, 4, 24, 24);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            Helpers.NativeMethods.DestroyIcon(hIcon);
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}
