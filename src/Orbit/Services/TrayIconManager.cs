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
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettings();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(showHideItem);
        menu.Items.Add(reloadItem);
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

    /// <summary>Draws a small filled-circle icon at runtime so the app doesn't need a shipped .ico asset.</summary>
    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(255, 0xD9, 0x77, 0x57)); // Claude-adjacent orange
            g.FillEllipse(brush, 4, 4, 24, 24);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
