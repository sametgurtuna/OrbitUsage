using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Orbit.Services;

/// <summary>
/// Owns a single WebView2 control backed by an isolated user-data folder (separate from the
/// user's real Edge/Chrome profile). The same control instance is reused for background scrapes
/// (hosted off-screen) and for the one-time manual login flow (temporarily re-parented into a
/// visible window) - reusing the instance keeps the same CoreWebView2Environment/cookies both ways.
/// </summary>
public class WebView2SessionManager : IAsyncDisposable
{
    private const int OffscreenSize = 1;
    private const int OffscreenPosition = -3000;

    public CoreWebView2Environment? Environment { get; private set; }
    public WebView2 SharedWebView { get; } = new();
    public bool IsInitialized { get; private set; }
    public string UserDataFolder { get; }
    public bool IsInteractiveSessionActive { get; private set; }

    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private Window? _hiddenHost;
    private Grid? _hiddenHostGrid;
    private Task<bool>? _initializeTask;

    public async Task<bool> TryAcquireSessionAsync(CancellationToken ct = default)
    {
        if (IsInteractiveSessionActive) return false;
        try
        {
            return await _sessionLock.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        catch
        {
            return false;
        }
    }

    public void ReleaseSession()
    {
        try { _sessionLock.Release(); } catch { }
    }

    public WebView2SessionManager()
    {
        UserDataFolder = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "Orbit", "WebView2");
        Directory.CreateDirectory(UserDataFolder);
    }

    /// <summary>
    /// Creates the isolated environment and initializes the shared control, hosted off-screen.
    /// Returns false (without throwing) if the WebView2 Runtime isn't installed, so callers can
    /// fall back to manual-mode-only operation.
    ///
    /// Safe to call concurrently - App.xaml.cs fires an unawaited warm-up call at startup while
    /// UsageScraperService's first refresh independently awaits its own call; without caching the
    /// in-flight task here, both would race CoreWebView2Environment.CreateAsync against the same
    /// user-data folder and one would lose (Chromium profile lock contention), surfacing as a
    /// spurious "WebView2 Runtime not available" on the very first refresh.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        var task = _initializeTask ??= InitializeCoreAsync();
        var result = await task;
        if (!result) _initializeTask = null; // let the next call retry rather than caching a failure forever
        return result;
    }

    private async Task<bool> InitializeCoreAsync()
    {
        try
        {
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }

        try
        {
            Environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: UserDataFolder,
                options: new CoreWebView2EnvironmentOptions());

            EnsureHiddenHost();
            _hiddenHostGrid!.Children.Add(SharedWebView);

            await SharedWebView.EnsureCoreWebView2Async(Environment);
            IsInitialized = true;
            return true;
        }
        catch
        {
            IsInitialized = false;
            return false;
        }
    }

    private void EnsureHiddenHost()
    {
        if (_hiddenHost != null) return;

        _hiddenHostGrid = new Grid();
        _hiddenHost = new Window
        {
            Title = "Orbit (background)",
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = false,
            Width = OffscreenSize,
            Height = OffscreenSize,
            Left = OffscreenPosition,
            Top = OffscreenPosition,
            Content = _hiddenHostGrid,
            ResizeMode = ResizeMode.NoResize,
            Owner = App.GetAltTabSuppressor()
        };
        _hiddenHost.SourceInitialized += (s, e) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_hiddenHost).Handle;
            Helpers.NativeMethods.MakeToolWindow(hwnd);
        };
        _hiddenHost.Show();
    }

    /// <summary>
    /// Temporarily re-parents the shared WebView2 control into a visible LoginWindow so the user
    /// can log into a service manually. Cookies land in the same isolated user-data folder used
    /// for background scrapes. Returns once the login window is closed by the user.
    /// </summary>
    public async Task ShowLoginWindowAsync(string url, Window? owner = null)
    {
        if (!IsInitialized)
        {
            var ok = await InitializeAsync();
            if (!ok) throw new InvalidOperationException("WebView2 Runtime is not available.");
        }

        await _sessionLock.WaitAsync();
        IsInteractiveSessionActive = true;
        try
        {
            _hiddenHostGrid!.Children.Remove(SharedWebView);

            var loginWindow = new Views.LoginWindow(SharedWebView);
            if (owner != null) loginWindow.Owner = owner;

            SharedWebView.CoreWebView2.Navigate(url);

            var tcs = new TaskCompletionSource();
            loginWindow.Closed += (_, _) => tcs.TrySetResult();
            loginWindow.Show();
            await tcs.Task;

            // Move the (still-initialized) control back to the hidden host for background polling.
            loginWindow.ReleaseWebView();
            if (!_hiddenHostGrid.Children.Contains(SharedWebView))
                _hiddenHostGrid.Children.Add(SharedWebView);
        }
        finally
        {
            IsInteractiveSessionActive = false;
            _sessionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            SharedWebView.Dispose();
        }
        catch
        {
            // best-effort cleanup
        }

        _hiddenHost?.Close();
        await Task.CompletedTask;
    }
}
