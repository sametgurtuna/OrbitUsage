using System.Windows;
using Microsoft.Win32;
using Orbit.Models;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Orbit.Helpers;

public static class ThemeManager
{
    private static NotchTheme _currentSetting = NotchTheme.Dark;
    private static bool _listeningToSystemEvents;

    public static void ApplyTheme(NotchTheme theme)
    {
        _currentSetting = theme;

        if (theme == NotchTheme.System)
        {
            if (!_listeningToSystemEvents)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                _listeningToSystemEvents = true;
            }
        }
        else if (_listeningToSystemEvents)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _listeningToSystemEvents = false;
        }

        bool isLight = theme switch
        {
            NotchTheme.Light => true,
            NotchTheme.System => IsWindowsSystemLight(),
            _ => false
        };

        ApplyBrushes(isLight);
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentSetting == NotchTheme.System)
        {
            Application.Current?.Dispatcher?.InvokeAsync(() => ApplyBrushes(IsWindowsSystemLight()));
        }
    }

    public static bool IsWindowsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intVal)
                return intVal == 1;
        }
        catch
        {
            // fallback
        }
        return false;
    }

    private static void ApplyBrushes(bool isLight)
    {
        var res = Application.Current?.Resources;
        if (res == null) return;

        if (isLight)
        {
            res["DockSilhouetteBrush"] = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF7));
            res["DockShadowColor"] = Color.FromRgb(0x00, 0x00, 0x00);
            res["DockShadowOpacity"] = 0.18;

            res["GaugeDiscBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["GaugeTrackBrush"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE8));
            res["GaugeIconBrush"] = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B));
            res["GaugeTextBrush"] = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B));

            res["FlyoutCardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["FlyoutCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE7));
            res["FlyoutCardShadowOpacity"] = 0.20;
            res["FlyoutTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x09, 0x09, 0x0B));
            res["FlyoutTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A));
            res["FlyoutTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A));
            res["FlyoutTextSubBrush"] = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x5B));
            res["FlyoutProgressBarTrackBrush"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE7));

            res["SettingsBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFB));
            res["SettingsCardBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["SettingsInnerCardBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF2, 0xF5));
            res["SettingsBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEB));
            res["SettingsTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
            res["SettingsTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            res["SettingsTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            res["SettingsTabHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF3));
            res["SettingsTabActiveBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["SettingsScrollThumbBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

            res["NotchBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xF2, 0xF6, 0xF8, 0xFA));
            res["NotchBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x00, 0x00));
            res["NotchTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
            res["NotchTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            res["NotchTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            res["BadgeBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x14, 0x00, 0x00, 0x00));
            res["ToolTipBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xF8, 0xFF, 0xFF, 0xFF));
            res["ToolTipBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            res["ToolTipTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
            res["ToolTipTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            res["ToolTipTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        }
        else
        {
            res["DockSilhouetteBrush"] = new SolidColorBrush(Color.FromRgb(0x06, 0x06, 0x08));
            res["DockShadowColor"] = Color.FromRgb(0x00, 0x00, 0x00);
            res["DockShadowOpacity"] = 0.80;

            res["GaugeDiscBrush"] = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x17));
            res["GaugeTrackBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26));
            res["GaugeIconBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["GaugeTextBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

            res["FlyoutCardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x11));
            res["FlyoutCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x28));
            res["FlyoutCardShadowOpacity"] = 0.80;
            res["FlyoutTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["FlyoutTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE6));
            res["FlyoutTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));
            res["FlyoutTextSubBrush"] = new SolidColorBrush(Color.FromRgb(0xD1, 0xD1, 0xD6));
            res["FlyoutProgressBarTrackBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26));

            res["SettingsBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x13, 0x14, 0x1B));
            res["SettingsCardBrush"] = new SolidColorBrush(Color.FromRgb(0x18, 0x1A, 0x24));
            res["SettingsInnerCardBrush"] = new SolidColorBrush(Color.FromRgb(0x12, 0x13, 0x1A));
            res["SettingsBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36));
            res["SettingsTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["SettingsTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            res["SettingsTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            res["SettingsTabHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x30));
            res["SettingsTabActiveBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36));
            res["SettingsScrollThumbBrush"] = new SolidColorBrush(Color.FromRgb(0x38, 0x3C, 0x50));

            res["NotchBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xFA, 0x06, 0x06, 0x08));
            res["NotchBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            res["NotchTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["NotchTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA3));
            res["NotchTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73));
            res["BadgeBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            res["ToolTipBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0xF8, 0x0A, 0x0A, 0x0C));
            res["ToolTipBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            res["ToolTipTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            res["ToolTipTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA));
            res["ToolTipTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A));
        }
    }
}
