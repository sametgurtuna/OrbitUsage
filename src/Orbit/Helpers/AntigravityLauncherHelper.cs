using System.Diagnostics;
using System.IO;

namespace Orbit.Helpers;

public static class AntigravityLauncherHelper
{
    private static readonly string[] SearchDirectories =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
    ];

    /// <summary>
    /// Locates the Antigravity IDE executable path on the system.
    /// </summary>
    public static string? FindExecutablePath()
    {
        // 1. Check if Antigravity is currently running
        try
        {
            var proc = Process.GetProcessesByName("Antigravity IDE")
                .Concat(Process.GetProcessesByName("Antigravity"))
                .FirstOrDefault();
            if (proc?.MainModule?.FileName is { } path && File.Exists(path))
                return path;
        }
        catch
        {
            // Process inspection might fail with access denied on certain systems
        }

        // 2. Check standard local AppData installation path
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidate1 = Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity IDE.exe");
        if (File.Exists(candidate1)) return candidate1;

        var candidate2 = Path.Combine(localAppData, "Programs", "Antigravity", "Antigravity.exe");
        if (File.Exists(candidate2)) return candidate2;

        // 3. Check Program Files
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidate3 = Path.Combine(programFiles, "Antigravity IDE", "Antigravity IDE.exe");
        if (File.Exists(candidate3)) return candidate3;

        return null;
    }

    /// <summary>
    /// Configures all desktop and Start Menu shortcuts (.lnk) for Antigravity to include
    /// --remote-debugging-port={port}.
    /// </summary>
    public static (int updatedCount, int totalFound) ConfigureShortcuts(int port = 9222)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            throw new InvalidOperationException("WScript.Shell COM object is not available on this Windows system.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        int updated = 0;
        int total = 0;

        foreach (var dir in SearchDirectories)
        {
            if (!Directory.Exists(dir)) continue;

            string[] lnkFiles;
            try
            {
                lnkFiles = Directory.GetFiles(dir, "*Antigravity*.lnk", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var lnk in lnkFiles)
            {
                try
                {
                    dynamic shortcut = shell.CreateShortcut(lnk);
                    string target = (string)shortcut.TargetPath;
                    if (!target.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    total++;
                    string args = ((string)shortcut.Arguments) ?? string.Empty;
                    string portArg = $"--remote-debugging-port={port}";

                    if (!args.Contains("--remote-debugging-port", StringComparison.OrdinalIgnoreCase))
                    {
                        shortcut.Arguments = string.IsNullOrWhiteSpace(args) ? portArg : $"{args} {portArg}";
                        shortcut.Save();
                        updated++;
                    }
                }
                catch
                {
                    // Ignore shortcuts that cannot be written (e.g. read-only permissions)
                }
            }
        }

        return (updated, total);
    }

    /// <summary>
    /// Launches Antigravity with the remote debugging port argument enabled.
    /// </summary>
    public static (bool success, string message) Launch(int port = 9222)
    {
        var exePath = FindExecutablePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return (false, "Could not locate Antigravity IDE executable path automatically.");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--remote-debugging-port={port}",
                UseShellExecute = true
            });
            return (true, $"Antigravity launched on port {port}.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to launch Antigravity: {ex.Message}");
        }
    }
}
