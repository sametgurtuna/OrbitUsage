using System.Runtime.InteropServices;

namespace Orbit.Helpers;

/// <summary>Thin P/Invoke wrapper used to apply WS_EX_TOOLWINDOW so the notch never appears in Alt-Tab.</summary>
internal static class NativeMethods
{
    internal const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
