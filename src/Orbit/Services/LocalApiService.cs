using System.Net;
using System.Text;
using System.Text.Json;
using Orbit.Models;
using Orbit.ViewModels;

namespace Orbit.Services;

/// <summary>
/// Lightweight local HTTP REST API server running on localhost (default: http://127.0.0.1:18923).
/// Exposes live usage data, reset timers, and manual refresh endpoints for Stream Deck plugins,
/// Rainmeter skins, CLI tools, and terminal scripts.
/// </summary>
public class LocalApiService : IAsyncDisposable
{
    private readonly NotchViewModel _viewModel;
    private readonly UsageScraperService? _scraper;
    private readonly SettingsService _settingsService;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public bool IsRunning => _listener?.IsListening ?? false;
    public int Port => _settingsService.Current.LocalApiPort;

    public LocalApiService(NotchViewModel viewModel, SettingsService settingsService, UsageScraperService? scraper = null)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _scraper = scraper;
    }

    public void Start()
    {
        if (!_settingsService.Current.EnableLocalApi) return;
        if (_listener != null) return;

        try
        {
            int port = Port > 0 ? Port : 18923;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            Serilog.Log.Information("[LocalApiService] HTTP server listening on http://127.0.0.1:{Port}/", port);
        }
        catch (Exception ex)
        {
            // Port might be in use or restricted; log and keep application alive
            Serilog.Log.Error(ex, "[LocalApiService] Failed to start HTTP listener on port {Port}", Port);
            _listener = null;
        }
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_listener != null)
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch { /* best effort */ }
            _listener = null;
        }

        if (_listenTask != null)
        {
            try { await _listenTask; } catch { /* ignore cancellation */ }
            _listenTask = null;
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalApiService] Context error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // Enable CORS for Stream Deck web actions, browser widgets, etc.
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = (int)HttpStatusCode.OK;
            res.Close();
            return;
        }

        string rawPath = req.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "";

        try
        {
            if (rawPath == "" || rawPath == "/index.html")
            {
                await ServeDashboardHtmlAsync(res);
            }
            else if (rawPath == "/api/usage")
            {
                await ServeAllUsageJsonAsync(res);
            }
            else if (rawPath.StartsWith("/api/usage/"))
            {
                string serviceKey = rawPath["/api/usage/".Length..];
                await ServeSingleUsageJsonAsync(res, serviceKey);
            }
            else if (rawPath == "/api/refresh" && req.HttpMethod == "POST")
            {
                await HandleRefreshAsync(res);
            }
            else if (rawPath == "/api/ascii" || rawPath == "/cli")
            {
                await ServeAsciiBannerAsync(res);
            }
            else if (rawPath == "/api/status")
            {
                await ServeStatusOverviewJsonAsync(res);
            }
            else
            {
                res.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteJsonAsync(res, new { error = "Not found", path = rawPath });
            }
        }
        catch (Exception ex)
        {
            res.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonAsync(res, new { error = ex.Message });
        }
        finally
        {
            try { res.Close(); } catch { }
        }
    }

    private async Task ServeAllUsageJsonAsync(HttpListenerResponse res)
    {
        var services = _viewModel.Services.Select(s => new
        {
            key = s.ServiceKey,
            name = s.DisplayName,
            percent = Math.Round(s.PercentUsed, 1),
            displayText = s.DisplayText,
            resetTime = s.ResetTimeText,
            resetDisplay = s.ResetDisplay,
            status = s.Status.ToString(),
            isActive = s.IsActive,
            color = s.AccentColorHex,
            lastUpdatedUtc = s.LastUpdatedUtc
        }).ToList();

        var payload = new
        {
            status = "ok",
            timestamp = DateTime.UtcNow,
            aggregateStatus = _viewModel.AggregateStatus.ToString(),
            services
        };

        await WriteJsonAsync(res, payload);
    }

    private async Task ServeSingleUsageJsonAsync(HttpListenerResponse res, string serviceKey)
    {
        var s = _viewModel.Services.FirstOrDefault(x => x.ServiceKey.Equals(serviceKey, StringComparison.OrdinalIgnoreCase));
        if (s == null)
        {
            res.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonAsync(res, new { error = $"Service '{serviceKey}' not found" });
            return;
        }

        var payload = new
        {
            key = s.ServiceKey,
            name = s.DisplayName,
            percent = Math.Round(s.PercentUsed, 1),
            displayText = s.DisplayText,
            resetTime = s.ResetTimeText,
            resetDisplay = s.ResetDisplay,
            status = s.Status.ToString(),
            isActive = s.IsActive,
            color = s.AccentColorHex,
            lastUpdatedUtc = s.LastUpdatedUtc
        };

        await WriteJsonAsync(res, payload);
    }

    public async Task RestartAsync(bool enabled, int port)
    {
        await StopAsync();
        if (enabled)
        {
            try
            {
                int p = port > 0 ? port : 18923;
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{p}/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalApiService] Failed to restart HTTP listener: {ex.Message}");
                _listener = null;
            }
        }
    }

    private async Task HandleRefreshAsync(HttpListenerResponse res)
    {
        if (_scraper != null)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await _scraper.RefreshNowAsync());
            }
            else
            {
                _ = _scraper.RefreshNowAsync();
            }
        }

        await WriteJsonAsync(res, new
        {
            status = "refreshing",
            message = "Triggered refresh across enabled providers"
        });
    }

    private async Task ServeStatusOverviewJsonAsync(HttpListenerResponse res)
    {
        await WriteJsonAsync(res, new
        {
            app = "Orbit",
            version = "0.2.0",
            aggregateStatus = _viewModel.AggregateStatus.ToString(),
            serviceCount = _viewModel.Services.Count,
            isRefreshing = _viewModel.IsRefreshing
        });
    }

    private async Task ServeAsciiBannerAsync(HttpListenerResponse res)
    {
        res.ContentType = "text/plain; charset=utf-8";
        string ascii = GenerateAsciiReport(_viewModel);
        byte[] bytes = Encoding.UTF8.GetBytes(ascii);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    public static string GenerateAsciiReport(NotchViewModel vm)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                     🛸 ORBIT - LLM USAGE MONITOR                    ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        foreach (var svc in vm.Services)
        {
            string bar = GenerateProgressBar(svc.PercentUsed, svc.IsActive, 24);
            string reset = !string.IsNullOrWhiteSpace(svc.ResetTimeText) ? $"[Reset: {svc.ResetTimeText}]" : "";
            string updated = svc.LastUpdatedUtc.HasValue ? $"{svc.LastUpdatedUtc.Value.ToLocalTime():HH:mm:ss}" : "--:--:--";

            sb.AppendLine($"  ● {svc.DisplayName,-13} {bar}  {svc.DisplayText,-7} {reset,-18} (Upd: {updated})");
        }

        sb.AppendLine();
        sb.AppendLine($"  API: http://127.0.0.1:18923/api/usage | Status: {vm.AggregateStatus}");
        sb.AppendLine("────────────────────────────────────────────────────────────────────────");
        return sb.ToString();
    }

    private static string GenerateProgressBar(double percent, bool isActive, int width)
    {
        if (!isActive)
            return $"[{new string('-', width)}]";

        int filled = Math.Clamp((int)Math.Round((percent / 100.0) * width), 0, width);
        int empty = width - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    private async Task ServeDashboardHtmlAsync(HttpListenerResponse res)
    {
        res.ContentType = "text/html; charset=utf-8";
        string html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Orbit - Local API Dashboard</title>
            <style>
                :root {
                    --bg: #0e0e11;
                    --card: #18181c;
                    --border: rgba(255,255,255,0.08);
                    --text: #f0f0f3;
                    --text-dim: #8e8e98;
                    --accent-claude: #D97757;
                    --accent-antigravity: #3B82F6;
                    --accent-chatgpt: #D8D8E0;
                    --success: #3DDC84;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                    background: var(--bg);
                    color: var(--text);
                    padding: 32px 20px;
                    display: flex;
                    justify-content: center;
                }
                .container { max-width: 780px; width: 100%; }
                .header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    margin-bottom: 24px;
                    padding-bottom: 16px;
                    border-bottom: 1px solid var(--border);
                }
                .title { font-size: 24px; font-weight: 700; display: flex; align-items: center; gap: 10px; }
                .badge {
                    background: rgba(61, 220, 132, 0.15);
                    color: var(--success);
                    border: 1px solid rgba(61, 220, 132, 0.3);
                    padding: 4px 10px;
                    border-radius: 999px;
                    font-size: 12px;
                    font-weight: 600;
                }
                .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
                .card {
                    background: var(--card);
                    border: 1px solid var(--border);
                    border-radius: 14px;
                    padding: 20px;
                    position: relative;
                    overflow: hidden;
                    box-shadow: 0 8px 24px rgba(0,0,0,0.3);
                }
                .card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
                .service-name { font-size: 15px; font-weight: 600; }
                .percent { font-size: 28px; font-weight: 800; }
                .progress-bg { height: 6px; background: rgba(255,255,255,0.06); border-radius: 3px; overflow: hidden; margin: 12px 0; }
                .progress-fill { height: 100%; border-radius: 3px; transition: width 0.5s ease; }
                .meta { font-size: 11px; color: var(--text-dim); display: flex; justify-content: space-between; }
                .endpoints { background: var(--card); border: 1px solid var(--border); border-radius: 14px; padding: 20px; }
                .endpoints h3 { font-size: 15px; margin-bottom: 12px; }
                .endpoint-row {
                    display: flex;
                    align-items: center;
                    gap: 10px;
                    padding: 8px 12px;
                    background: rgba(0,0,0,0.25);
                    border-radius: 8px;
                    margin-bottom: 8px;
                    font-family: monospace;
                    font-size: 12px;
                }
                .method { background: #2563EB; color: white; padding: 2px 6px; border-radius: 4px; font-weight: 700; font-size: 10px; }
                .method.post { background: #059669; }
                a { color: #60A5FA; text-decoration: none; }
                a:hover { text-decoration: underline; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <div class="title">🛸 Orbit Local API</div>
                    <span class="badge">● Live & Listening</span>
                </div>

                <div class="grid" id="servicesGrid">
                    <!-- Loaded dynamically via JS -->
                </div>

                <div class="endpoints">
                    <h3>🔌 Integration Endpoints (Stream Deck / Rainmeter / cURL)</h3>
                    <div class="endpoint-row">
                        <span class="method">GET</span>
                        <a href="/api/usage" target="_blank">/api/usage</a>
                        <span style="color: var(--text-dim); margin-left: auto;">Full JSON quota state</span>
                    </div>
                    <div class="endpoint-row">
                        <span class="method">GET</span>
                        <a href="/api/usage/claude" target="_blank">/api/usage/claude</a>
                        <span style="color: var(--text-dim); margin-left: auto;">Single service data</span>
                    </div>
                    <div class="endpoint-row">
                        <span class="method">GET</span>
                        <a href="/api/ascii" target="_blank">/api/ascii</a>
                        <span style="color: var(--text-dim); margin-left: auto;">Terminal banner for curl</span>
                    </div>
                    <div class="endpoint-row">
                        <span class="method post">POST</span>
                        <span>/api/refresh</span>
                        <span style="color: var(--text-dim); margin-left: auto;">Trigger instant scrape</span>
                    </div>
                </div>
            </div>

            <script>
                async function fetchUsage() {
                    try {
                        const res = await fetch('/api/usage');
                        const data = await res.json();
                        const grid = document.getElementById('servicesGrid');
                        grid.innerHTML = data.services.map(s => `
                            <div class="card" style="border-top: 3px solid ${s.color};">
                                <div class="card-header">
                                    <span class="service-name">${s.name}</span>
                                    <span style="font-size:11px; color:${s.color}; font-weight:600;">${s.status}</span>
                                </div>
                                <div class="percent" style="color: ${s.color}">${s.displayText}</div>
                                <div class="progress-bg">
                                    <div class="progress-fill" style="width: ${Math.min(100, s.percent)}%; background: ${s.color}"></div>
                                </div>
                                <div class="meta">
                                    <span>${s.resetDisplay || 'No timer'}</span>
                                    <span>${s.lastUpdatedUtc ? new Date(s.lastUpdatedUtc).toLocaleTimeString() : 'Pending'}</span>
                                </div>
                            </div>
                        `).join('');
                    } catch (e) {
                        console.error(e);
                    }
                }
                fetchUsage();
                setInterval(fetchUsage, 4000);
            </script>
        </body>
        </html>
        """;

        byte[] bytes = Encoding.UTF8.GetBytes(html);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, object data)
    {
        res.ContentType = "application/json; charset=utf-8";
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
