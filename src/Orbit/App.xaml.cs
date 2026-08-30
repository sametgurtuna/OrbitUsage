using System.Windows;
using Orbit.Services;
using Orbit.ViewModels;
using Orbit.Views;

namespace Orbit;

/// <summary>
/// Wires up settings/selectors loading, the WebView2 session, the scraper service, the notch
/// window, and the tray icon. Closing/hiding the notch window must not terminate the app - only
/// the tray "Exit" command does that (ShutdownMode.OnExplicitShutdown).
/// </summary>
public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private WebView2SessionManager? _session;
    private UsageScraperService? _scraper;
    private TrayIconManager? _trayIconManager;
    private SettingsService? _settingsService;
    private SelectorConfigService? _selectorService;

    private static readonly string CrashLogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbit", "crash.log");

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = true;
        };

        _settingsService = new SettingsService();
        _settingsService.Load();

        _selectorService = new SelectorConfigService();
        _selectorService.Load();

        var viewModel = new NotchViewModel(_settingsService.Current);
        _mainWindow = new MainWindow(viewModel, _settingsService);
        _mainWindow.ApplyLayout(_settingsService.Current.Layout, _settingsService.Current.TargetMonitorDeviceName);
        _mainWindow.Show();

        _session = new WebView2SessionManager();

        var providers = new IUsageProvider[]
        {
            new ClaudeUsageProvider(),
            new ChatGptUsageProvider(),
            new AntigravityUsageProvider(),
        };
        _scraper = new UsageScraperService(_session, _settingsService, _selectorService, providers);

        _trayIconManager = new TrayIconManager(
            _mainWindow,
            _scraper,
            () => new SettingsWindow(_settingsService, _selectorService, _session, _scraper, _mainWindow));

        var notificationService = new NotificationService(_trayIconManager, _settingsService);
        _scraper.UsageUpdated += (key, result) =>
        {
            viewModel.ApplyUsageUpdate(key, result);
            notificationService.CheckAndNotify(key, result);
        };
        _scraper.RefreshingChanged += refreshing => viewModel.IsRefreshing = refreshing;
        viewModel.RefreshRequested += async () => await _scraper.RefreshNowAsync();

        // Initialize WebView2 in the background; if the runtime isn't installed, RefreshOneAsync
        // will surface that as a per-service error rather than blocking startup.
        _ = _session.InitializeAsync();

        _scraper.Start();
        await _scraper.RefreshNowAsync();
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(CrashLogPath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(CrashLogPath, $"[{DateTime.Now:O}]\n{ex}\n\n");
        }
        catch { /* best-effort diagnostics only */ }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        if (_session != null)
            await _session.DisposeAsync();
        base.OnExit(e);
    }
}
