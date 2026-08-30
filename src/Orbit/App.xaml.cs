using System.Windows;
using Orbit.Helpers;
using Orbit.Services;
using Orbit.ViewModels;
using Orbit.Views;

namespace Orbit;

/// <summary>
/// Wires up settings/selectors loading, the WebView2 session, the scraper service, the notch
/// window, the tray icon, and the local REST API server.
/// </summary>
public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private WebView2SessionManager? _session;
    private UsageScraperService? _scraper;
    private TrayIconManager? _trayIconManager;
    private SettingsService? _settingsService;
    private SelectorConfigService? _selectorService;
    private LocalApiService? _localApi;

    private static readonly string CrashLogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbit", "crash.log");

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        // Check if user ran a CLI command (e.g. orbit status, orbit refresh, orbit --help)
        if (e.Args.Length > 0 && await HandleCliCommandAsync(e.Args))
        {
            Shutdown(0);
            return;
        }

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

        // Start Local REST API server (Stream Deck, Rainmeter, CLI, curl)
        _localApi = new LocalApiService(viewModel, _settingsService, _scraper);
        _localApi.Start();

        // Initialize WebView2 in the background
        _ = _session.InitializeAsync();

        _scraper.Start();
        await _scraper.RefreshNowAsync();
    }

    private static async Task<bool> HandleCliCommandAsync(string[] args)
    {
        if (args.Length == 0) return false;

        string cmd = args[0].ToLowerInvariant();
        if (cmd is not ("status" or "refresh" or "ascii" or "help" or "--help" or "-h" or "-v" or "--version" or "--json"))
            return false;

        if (NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS))
        {
            var writer = new System.IO.StreamWriter(Console.OpenStandardOutput(), System.Text.Encoding.UTF8) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
        }
        Console.WriteLine();

        if (cmd is "help" or "--help" or "-h")
        {
            Console.WriteLine("🛸 Orbit CLI Usage:");
            Console.WriteLine("  orbit status          - Print current LLM quota status & timers");
            Console.WriteLine("  orbit status --json   - Output raw JSON format (for scripts)");
            Console.WriteLine("  orbit refresh         - Trigger immediate background refresh");
            Console.WriteLine("  orbit --version       - Show version info");
            Console.WriteLine("  orbit                 - Launch floating desktop notch GUI");
            Console.WriteLine();
            Console.WriteLine("Local REST API: http://127.0.0.1:18923/api/usage");
            Console.WriteLine();
            return true;
        }

        if (cmd is "-v" or "--version")
        {
            Console.WriteLine("Orbit version 1.0.0 (Windows x64)");
            return true;
        }

        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        if (cmd == "refresh")
        {
            try
            {
                var response = await client.PostAsync("http://127.0.0.1:18923/api/refresh", null);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✔ Refresh triggered successfully on running Orbit instance.");
                }
                else
                {
                    Console.WriteLine($"✖ Orbit API returned status: {response.StatusCode}");
                }
            }
            catch
            {
                Console.WriteLine("✖ Could not reach Orbit API on 127.0.0.1:18923. Is Orbit running?");
            }
            return true;
        }

        bool wantsJson = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));

        try
        {
            if (wantsJson)
            {
                string json = await client.GetStringAsync("http://127.0.0.1:18923/api/usage");
                Console.WriteLine(json);
            }
            else
            {
                string ascii = await client.GetStringAsync("http://127.0.0.1:18923/api/ascii");
                Console.WriteLine(ascii);
            }
        }
        catch
        {
            // If API isn't running, read cached settings.json directly!
            var settingsService = new SettingsService();
            settingsService.Load();
            var vm = new NotchViewModel(settingsService.Current);

            if (wantsJson)
            {
                var services = vm.Services.Select(s => new
                {
                    key = s.ServiceKey,
                    name = s.DisplayName,
                    percent = s.PercentUsed,
                    displayText = s.DisplayText,
                    resetTime = s.ResetTimeText,
                    status = s.Status.ToString(),
                    lastUpdatedUtc = s.LastUpdatedUtc
                });
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(services, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(LocalApiService.GenerateAsciiReport(vm));
                Console.WriteLine("  (Note: Orbit background app is not running; showing cached values)");
            }
        }

        return true;
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
        if (_localApi != null)
            await _localApi.DisposeAsync();

        _trayIconManager?.Dispose();
        if (_session != null)
            await _session.DisposeAsync();
        base.OnExit(e);
    }
}
