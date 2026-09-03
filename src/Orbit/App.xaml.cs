using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orbit.Helpers;
using Orbit.Services;
using Orbit.ViewModels;
using Orbit.Views;
using Serilog;

namespace Orbit;

/// <summary>
/// Wires up settings/selectors loading, the WebView2 session, the scraper service, the notch
/// window, the tray icon, and the local REST API server via Microsoft.Extensions.Hosting and Serilog.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private MainWindow? _mainWindow;
    private WebView2SessionManager? _session;
    private UsageScraperService? _scraper;
    private TrayIconManager? _trayIconManager;
    private LocalApiService? _localApi;
    private NotchViewModel? _viewModel;

    private static Window? _altTabSuppressor;

    /// <summary>
    /// Creates or returns a permanent, unrendered hidden tool window used as the owner
    /// of floating desktop windows so Windows DWM completely excludes them from Alt-Tab.
    /// </summary>
    public static Window? GetAltTabSuppressor()
    {
        try
        {
            if (_altTabSuppressor == null)
            {
                _altTabSuppressor = new Window
                {
                    Width = 0,
                    Height = 0,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -32000,
                    Top = -32000,
                    Visibility = Visibility.Hidden
                };
                var helper = new System.Windows.Interop.WindowInteropHelper(_altTabSuppressor);
                helper.EnsureHandle();
                NativeMethods.MakeToolWindow(helper.Handle);
            }
            return _altTabSuppressor;
        }
        catch
        {
            return null;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 1. Configure Serilog structured rolling file logger
        ConfigureLogging();

        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = true;
        };

        // 2. Fast-path CLI command execution (e.g. orbit status, orbit refresh, orbit --help)
        if (e.Args.Length > 0 && await HandleCliCommandAsync(e.Args))
        {
            await Log.CloseAndFlushAsync();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Log.Information("[App] Starting Orbit desktop notch application...");

        // 3. Build and initialize Microsoft.Extensions.Hosting DI container
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                // Configuration Services
                services.AddSingleton<SettingsService>(sp =>
                {
                    var svc = new SettingsService();
                    svc.Load();
                    return svc;
                });
                services.AddSingleton<SelectorConfigService>(sp =>
                {
                    var svc = new SelectorConfigService();
                    svc.Load();
                    return svc;
                });

                // WebView2 Session
                services.AddSingleton<WebView2SessionManager>();

                // Scraper Providers
                services.AddSingleton<IUsageProvider, ClaudeUsageProvider>();
                services.AddSingleton<IUsageProvider, ChatGptUsageProvider>();
                services.AddSingleton<IUsageProvider, AntigravityUsageProvider>();

                // Scraper Service
                services.AddSingleton<UsageScraperService>();

                // ViewModels
                services.AddSingleton<NotchViewModel>(sp =>
                {
                    var settings = sp.GetRequiredService<SettingsService>().Current;
                    return new NotchViewModel(settings);
                });
                services.AddTransient<SettingsViewModel>();

                // Views
                services.AddSingleton<MainWindow>(sp => new MainWindow(
                    sp.GetRequiredService<NotchViewModel>(),
                    sp.GetRequiredService<SettingsService>(),
                    () => sp.GetRequiredService<SettingsWindow>()));
                services.AddTransient<SettingsWindow>();

                // System & Tray & API Services
                services.AddSingleton<TrayIconManager>(sp =>
                {
                    var main = sp.GetRequiredService<MainWindow>();
                    var scraper = sp.GetRequiredService<UsageScraperService>();
                    return new TrayIconManager(main, scraper, () => sp.GetRequiredService<SettingsWindow>());
                });
                services.AddSingleton<NotificationService>();
                services.AddSingleton<LocalApiService>();
            })
            .Build();

        await _host.StartAsync();

        var sp = _host.Services;
        var settingsService = sp.GetRequiredService<SettingsService>();
        ThemeManager.ApplyTheme(settingsService.Current.Theme);

        _viewModel = sp.GetRequiredService<NotchViewModel>();
        _mainWindow = sp.GetRequiredService<MainWindow>();
        _mainWindow.ApplyLayout(settingsService.Current.Layout, settingsService.Current.TargetMonitorDeviceName);
        _mainWindow.Show();

        _session = sp.GetRequiredService<WebView2SessionManager>();
        _scraper = sp.GetRequiredService<UsageScraperService>();
        _trayIconManager = sp.GetRequiredService<TrayIconManager>();
        var notificationService = sp.GetRequiredService<NotificationService>();

        _scraper.UsageUpdated += (key, result) =>
        {
            _viewModel.ApplyUsageUpdate(key, result);
            notificationService.CheckAndNotify(key, result);
        };
        _scraper.RefreshingChanged += refreshing => _viewModel.IsRefreshing = refreshing;
        _viewModel.RefreshRequested += async () => await _scraper.RefreshNowAsync();

        // Start Local REST API server (Stream Deck, Rainmeter, CLI, curl)
        _localApi = sp.GetRequiredService<LocalApiService>();
        _localApi.Start();

        // Initialize WebView2 in the background
        _ = _session.InitializeAsync();

        _scraper.Start();
        await _scraper.RefreshNowAsync();
    }

    private static void ConfigureLogging()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDir = Path.Combine(localAppData, "Orbit", "logs");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "orbit-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("[App] Logging initialized");
        }
        catch
        {
            // Silently fall back if filesystem logging is restricted
        }
    }

    private static async Task<bool> HandleCliCommandAsync(string[] args)
    {
        if (args.Length == 0) return false;

        string cmd = args[0].ToLowerInvariant();
        if (cmd is not ("status" or "refresh" or "ascii" or "help" or "--help" or "-h" or "-v" or "--version" or "--json"))
            return false;

        try
        {
            if (!Console.IsOutputRedirected)
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
            }
            var stdOutHandle = NativeMethods.GetStdHandle(-11);
            if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
            {
                var fsOut = new System.IO.FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, false), System.IO.FileAccess.Write);
                var writer = new System.IO.StreamWriter(fsOut, System.Text.Encoding.UTF8) { AutoFlush = true };
                Console.SetOut(writer);
            }
            else
            {
                var writer = new System.IO.StreamWriter(Console.OpenStandardOutput(), System.Text.Encoding.UTF8) { AutoFlush = true };
                Console.SetOut(writer);
            }
        }
        catch { }
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
            Console.WriteLine("Orbit version 0.3.0 (Windows x64)");
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
        Log.Fatal(ex, "[App] Unhandled application exception caught");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("[App] Orbit is exiting cleanly...");

        if (_localApi != null)
            await _localApi.DisposeAsync();

        _trayIconManager?.Dispose();
        if (_session != null)
            await _session.DisposeAsync();

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
