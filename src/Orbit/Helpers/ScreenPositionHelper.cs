using System.Windows;
using System.Windows.Media;
using Orbit.Models;

namespace Orbit.Helpers;

public class MonitorInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Sizes and positions the notch window for the current layout and chosen monitor, DPI-aware.
/// The window itself is never animated; it is sized once (per layout) to the layout's max
/// expanded footprint and pinned to the anchor edge. Only the inner NotchBorder animates its
/// Width/Height inside that fixed window.
/// </summary>
public static class ScreenPositionHelper
{
    public const double TopCenterWindowWidth = 600;
    public const double TopCenterWindowHeight = 320;

    public const double RightCenterWindowWidth = 380;
    public const double RightCenterWindowHeight = 520;

    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            string name = s.Primary
                ? $"Monitor {i + 1} (Primary - {s.Bounds.Width}x{s.Bounds.Height})"
                : $"Monitor {i + 1} ({s.Bounds.Width}x{s.Bounds.Height} at {s.Bounds.Left},{s.Bounds.Top})";
            list.Add(new MonitorInfo
            {
                DeviceName = s.DeviceName,
                DisplayName = name,
                IsPrimary = s.Primary
            });
        }
        return list;
    }

    public static (double Left, double Top, double Width, double Height) CalculateWindowBounds(
        NotchLayout layout,
        double workAreaLeft,
        double workAreaTop,
        double workAreaWidth,
        double workAreaHeight,
        double dpiScale = 1.0,
        double offsetX = 0,
        double offsetY = 0)
    {
        double left = workAreaLeft / dpiScale;
        double top = workAreaTop / dpiScale;
        double width = workAreaWidth / dpiScale;
        double height = workAreaHeight / dpiScale;

        if (layout == NotchLayout.RightCenter)
        {
            double w = RightCenterWindowWidth;
            double h = RightCenterWindowHeight;
            double l = left + width - w + offsetX;
            double t = top + (height - h) / 2 + offsetY;
            return (l, t, w, h);
        }
        else
        {
            double w = TopCenterWindowWidth;
            double h = TopCenterWindowHeight;
            double l = left + (width - w) / 2 + offsetX;
            double t = top + offsetY;
            return (l, t, w, h);
        }
    }

    public static void Position(Window window, NotchLayout layout, string? targetMonitorDeviceName = null, double offsetX = 0, double offsetY = 0)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0) return;

        var screen = screens.FirstOrDefault(s => !string.IsNullOrWhiteSpace(targetMonitorDeviceName) && s.DeviceName == targetMonitorDeviceName)
                     ?? System.Windows.Forms.Screen.PrimaryScreen
                     ?? screens[0];

        double dpiScale = 1.0;
        try
        {
            dpiScale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        }
        catch (InvalidOperationException)
        {
            // Window not yet composed (no HwndSource) - fall back to 1.0 (96 DPI) for this pass;
            // callers should re-invoke after SourceInitialized for an accurate value.
        }

        var (winLeft, winTop, winWidth, winHeight) = CalculateWindowBounds(
            layout,
            screen.WorkingArea.Left,
            screen.WorkingArea.Top,
            screen.WorkingArea.Width,
            screen.WorkingArea.Height,
            dpiScale,
            offsetX,
            offsetY);

        window.Width = winWidth;
        window.Height = winHeight;
        window.Left = winLeft;
        window.Top = winTop;
    }
}
