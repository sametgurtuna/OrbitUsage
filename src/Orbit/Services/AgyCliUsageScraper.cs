using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Orbit.Models;
using Serilog;

namespace Orbit.Services;

/// <summary>
/// Scrapes Antigravity's model quota via Google's official Antigravity CLI ('agy.exe').
/// Runs 'agy -p "/usage" --output-format json' silently in the background with no console window.
/// Reads exact remaining quota fractions and ISO reset timestamps for Gemini models.
/// </summary>
internal static class AgyCliUsageScraper
{
    private static string? _cachedAgyPath;

    public static string? ResolveAgyPath()
    {
        if (!string.IsNullOrEmpty(_cachedAgyPath) && File.Exists(_cachedAgyPath))
            return _cachedAgyPath;

        // 1. Check standard AppData local install path
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string defaultPath = Path.Combine(localAppData, "agy", "bin", "agy.exe");
        if (File.Exists(defaultPath))
        {
            _cachedAgyPath = defaultPath;
            return defaultPath;
        }

        // 2. Check PATH environment variable
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), "agy.exe");
                    if (File.Exists(candidate))
                    {
                        _cachedAgyPath = candidate;
                        return candidate;
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static bool _isInstalling = false;

    public static async Task<bool> TryAutoInstallAgyAsync(CancellationToken ct = default)
    {
        if (_isInstalling) return false;
        _isInstalling = true;
        try
        {
            Log.Information("[AgyCliUsageScraper] Antigravity CLI not found. Attempting silent auto-install via official script...");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://antigravity.google/cli/install.ps1 | iex\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            if (process.Start())
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                await process.WaitForExitAsync(cts.Token);
            }

            _cachedAgyPath = null;
            string? installedPath = ResolveAgyPath();
            bool success = !string.IsNullOrEmpty(installedPath) && File.Exists(installedPath);
            if (success)
            {
                Log.Information("[AgyCliUsageScraper] Antigravity CLI installed successfully to {Path}", installedPath);
            }
            return success;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AgyCliUsageScraper] Auto-install of Antigravity CLI failed");
            return false;
        }
        finally
        {
            _isInstalling = false;
        }
    }

    public static async Task<UsageResult> ScrapeAsync(CancellationToken ct)
    {
        string? agyPath = ResolveAgyPath();
        if (string.IsNullOrEmpty(agyPath))
        {
            // Auto-install in the background if missing
            bool installed = await TryAutoInstallAgyAsync(ct);
            if (installed)
            {
                agyPath = ResolveAgyPath();
            }

            if (string.IsNullOrEmpty(agyPath))
            {
                return UsageResult.Fail("Antigravity CLI (agy.exe) not found. Run 'irm https://antigravity.google/cli/install.ps1 | iex' in PowerShell.");
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = agyPath,
                Arguments = "-p \"/usage\" --output-format json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                return UsageResult.Fail("Failed to start agy.exe process.");
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(15));

            string stdout = await process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token);

            return ParseUsageJson(stdout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AgyCliUsageScraper] Exception executing agy.exe");
            return UsageResult.Fail($"agy CLI scraping error: {ex.Message}");
        }
    }

    public static UsageResult ParseUsageJson(string stdout, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return UsageResult.Fail("agy.exe returned empty output.");
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            if (!root.TryGetProperty("command", out var cmdProp) ||
                !cmdProp.TryGetProperty("data", out var dataProp) ||
                !dataProp.TryGetProperty("groups", out var groupsProp) ||
                groupsProp.ValueKind != JsonValueKind.Array)
            {
                return UsageResult.Fail("Unexpected JSON structure from agy /usage command.");
            }

            DateTime currentUtc = nowUtc ?? DateTime.UtcNow;

            // Target the "Gemini Models" group first, falling back to any valid group
            JsonElement? targetGroup = null;
            foreach (var group in groupsProp.EnumerateArray())
            {
                string groupName = group.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                if (groupName.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    targetGroup = group;
                    break;
                }
            }

            if (!targetGroup.HasValue)
            {
                foreach (var group in groupsProp.EnumerateArray())
                {
                    if (group.TryGetProperty("buckets", out var b) && b.ValueKind == JsonValueKind.Array && b.GetArrayLength() > 0)
                    {
                        targetGroup = group;
                        break;
                    }
                }
            }

            if (!targetGroup.HasValue ||
                !targetGroup.Value.TryGetProperty("buckets", out var bucketsProp) ||
                bucketsProp.ValueKind != JsonValueKind.Array)
            {
                return UsageResult.Fail("No quota buckets found in agy output.");
            }

            JsonElement? weeklyBucket = null;
            JsonElement? fiveHourBucket = null;
            JsonElement? firstBucket = null;

            foreach (var bucket in bucketsProp.EnumerateArray())
            {
                firstBucket ??= bucket;
                string window = bucket.TryGetProperty("window", out var winProp) ? winProp.GetString() ?? "" : "";
                string bucketId = bucket.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                string bucketName = bucket.TryGetProperty("name", out var bnProp) ? bnProp.GetString() ?? "" : "";

                if (window.Equals("weekly", StringComparison.OrdinalIgnoreCase) ||
                    bucketId.Contains("weekly", StringComparison.OrdinalIgnoreCase) ||
                    bucketName.Contains("Weekly", StringComparison.OrdinalIgnoreCase))
                {
                    weeklyBucket = bucket;
                }
                else if (window.Equals("5h", StringComparison.OrdinalIgnoreCase) ||
                         bucketId.Contains("5h", StringComparison.OrdinalIgnoreCase) ||
                         bucketId.Contains("five", StringComparison.OrdinalIgnoreCase) ||
                         bucketName.Contains("Five Hour", StringComparison.OrdinalIgnoreCase))
                {
                    fiveHourBucket = bucket;
                }
            }

            var primaryBucket = weeklyBucket ?? firstBucket;
            if (primaryBucket.HasValue)
            {
                var weeklyData = ExtractBucket(primaryBucket.Value, currentUtc);
                if (weeklyData.HasValue)
                {
                    double? sessionPercent = null;
                    string? sessionReset = null;

                    if (fiveHourBucket.HasValue)
                    {
                        var sessionData = ExtractBucket(fiveHourBucket.Value, currentUtc);
                        if (sessionData.HasValue)
                        {
                            sessionPercent = Math.Round(sessionData.Value.percentUsed, 1);
                            sessionReset = sessionData.Value.resetText;
                        }
                    }

                    return UsageResult.Ok(
                        percentUsed: Math.Round(weeklyData.Value.percentUsed, 1),
                        rawText: $"{Math.Round(weeklyData.Value.percentUsed)}%",
                        resetTimeText: weeklyData.Value.resetText,
                        sessionPercentUsed: sessionPercent,
                        sessionResetTimeText: sessionReset);
                }
            }

            return UsageResult.Fail("No quota buckets found in agy output.");
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "[AgyCliUsageScraper] Failed to parse JSON output from agy");
            return UsageResult.Fail($"Failed to parse JSON: {ex.Message}");
        }
    }

    private static (double percentUsed, string? resetText)? ExtractBucket(JsonElement bucket, DateTime currentUtc)
    {
        if (!bucket.TryGetProperty("remaining_fraction", out var remProp))
            return null;

        double remainingFraction = remProp.GetDouble();
        double usedPercent = Math.Clamp((1.0 - remainingFraction) * 100.0, 0, 100);

        string? resetText = null;
        if (bucket.TryGetProperty("reset_time", out var resetTimeProp) &&
            DateTime.TryParse(resetTimeProp.GetString(), out var resetUtc))
        {
            var span = resetUtc.ToUniversalTime() - currentUtc;
            if (span.TotalMinutes > 0)
            {
                int days = (int)span.TotalDays;
                int hours = span.Hours;
                int minutes = span.Minutes;

                if (days > 0)
                    resetText = $"in {days}d {hours}h";
                else if (hours > 0)
                    resetText = $"in {hours}h {minutes}m";
                else
                    resetText = $"in {minutes}m";
            }
        }

        return (usedPercent, resetText);
    }
}
