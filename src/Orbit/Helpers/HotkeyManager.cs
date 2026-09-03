using System.Windows.Input;
using Orbit.Models;

namespace Orbit.Helpers;

public static class HotkeyManager
{
    public const int HotkeyId = 9001;
    public const int HotkeyIdAltOnly = 9002;

    public static bool Register(IntPtr hWnd, AppSettings settings)
    {
        if (hWnd == IntPtr.Zero) return false;

        // Unregister existing first to prevent duplicates
        NativeMethods.UnregisterHotKey(hWnd, HotkeyId);
        NativeMethods.UnregisterHotKey(hWnd, HotkeyIdAltOnly);

        if (!settings.HotkeyEnabled) return true;

        uint modifiers = ParseModifiers(settings.HotkeyModifiers) | NativeMethods.MOD_NOREPEAT;
        uint vk = ParseKey(settings.HotkeyKey);

        if (vk == 0) return false;

        bool ok = NativeMethods.RegisterHotKey(hWnd, HotkeyId, modifiers, vk);

        // Also register Alt+Key if primary was Win+Alt, or vice-versa, so both Alt+O and Win+Alt+O work
        uint altOnly = NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
        if (modifiers != altOnly)
        {
            NativeMethods.RegisterHotKey(hWnd, HotkeyIdAltOnly, altOnly, vk);
        }

        return ok;
    }

    public static void Unregister(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(hWnd, HotkeyId);
            NativeMethods.UnregisterHotKey(hWnd, HotkeyIdAltOnly);
        }
    }

    public static uint ParseModifiers(string? modifierStr)
    {
        uint mods = 0;
        if (string.IsNullOrWhiteSpace(modifierStr))
            return NativeMethods.MOD_WIN | NativeMethods.MOD_ALT;

        var parts = modifierStr.Split(new[] { '+', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case "win":
                case "windows":
                    mods |= NativeMethods.MOD_WIN;
                    break;
                case "alt":
                    mods |= NativeMethods.MOD_ALT;
                    break;
                case "ctrl":
                case "control":
                    mods |= NativeMethods.MOD_CONTROL;
                    break;
                case "shift":
                    mods |= NativeMethods.MOD_SHIFT;
                    break;
            }
        }

        return mods == 0 ? (NativeMethods.MOD_WIN | NativeMethods.MOD_ALT) : mods;
    }

    public static uint ParseKey(string? keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr))
            keyStr = "O";

        if (Enum.TryParse<Key>(keyStr.Trim(), true, out var key))
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk > 0) return (uint)vk;
        }

        // Fallback for single characters
        char c = keyStr.Trim().ToUpperInvariant()[0];
        if (c >= 'A' && c <= 'Z') return (uint)c;
        if (c >= '0' && c <= '9') return (uint)c;

        return (uint)'O';
    }
}
