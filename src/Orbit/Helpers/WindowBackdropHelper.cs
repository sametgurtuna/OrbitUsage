using System.Windows;
using System.Windows.Interop;

namespace Orbit.Helpers;

public static class WindowBackdropHelper
{
    public const int BackdropMica = 2;
    public const int BackdropAcrylic = 3;
    public const int BackdropMicaAlt = 4;

    public static void ApplyBackdrop(Window window, bool isDark = true, int backdropType = BackdropAcrylic)
    {
        // Windows 11 DWM system backdrops (Mica / Acrylic) are fundamentally incompatible with
        // WPF windows using AllowsTransparency="True". DWM renders the backdrop across the entire
        // rectangular HWND area instead of honoring per-pixel alpha, turning transparent canvas regions
        // into a solid grey box.
        if (window.AllowsTransparency) return;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1. Apply Immersive Dark Mode
            int darkMode = isDark ? 1 : 0;
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // 2. Apply Windows 11 Backdrop (Acrylic / Mica)
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int backdrop = backdropType;
                _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
        }
        catch
        {
            // Silently fall back to standard WPF composition on unsupported OS versions
        }
    }
}
