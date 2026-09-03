using Orbit.Models;
using Orbit.Services;
using Orbit.ViewModels;
using Xunit;

namespace Orbit.Tests.ViewModels;

public class SettingsViewModelTests
{
    private SettingsService CreateSettings()
    {
        var settings = new SettingsService();
        return settings;
    }

    [Fact]
    public void Constructor_LoadsInitialValuesFromSettings()
    {
        var settings = CreateSettings();
        settings.Current.Layout = NotchLayout.RightCenter;
        settings.Current.Theme = NotchTheme.Light;
        settings.Current.AnimationSpeed = NotchAnimationSpeed.Fast;
        settings.Current.NotchOffsetX = 15;
        settings.Current.RefreshIntervalMinutes = 30;

        var vm = new SettingsViewModel(settings);

        Assert.True(vm.IsLayoutRightCenter);
        Assert.False(vm.IsLayoutTopCenter);
        Assert.True(vm.IsThemeLight);
        Assert.False(vm.IsThemeDark);
        Assert.True(vm.IsSpeedFast);
        Assert.Equal(15, vm.NotchOffsetX);
        Assert.Equal(30, vm.RefreshIntervalMinutes);
    }

    [Theory]
    [InlineData(120, 100)]
    [InlineData(-50, 0)]
    [InlineData(45, 45)]
    public void ClaudeManualPercent_ClampsToValidRange(double input, double expected)
    {
        var settings = CreateSettings();
        var vm = new SettingsViewModel(settings);

        vm.ClaudeManualPercent = input;

        Assert.Equal(expected, vm.ClaudeManualPercent);
    }

    [Theory]
    [InlineData(150, 120)]
    [InlineData(2, 5)]
    [InlineData(45, 45)]
    public void RefreshIntervalMinutes_ClampsToAllowedWindow(int input, int expected)
    {
        var settings = CreateSettings();
        var vm = new SettingsViewModel(settings);

        vm.RefreshIntervalMinutes = input;

        Assert.Equal(expected, vm.RefreshIntervalMinutes);
    }

    [Fact]
    public void HotkeySetting_UpdatesPreviewText()
    {
        var settings = CreateSettings();
        var vm = new SettingsViewModel(settings);

        vm.HotkeyModifiers = "Ctrl+Shift";
        vm.HotkeyKey = "k";

        Assert.Equal("Ctrl+Shift + K", vm.HotkeyPreviewText);
    }

    [Fact]
    public void HelperBooleans_SynchronizeWithUnderlyingEnums()
    {
        var settings = CreateSettings();
        var vm = new SettingsViewModel(settings);

        vm.IsLayoutTopCenter = true;
        Assert.Equal(NotchLayout.TopCenter, vm.Layout);
        Assert.False(vm.IsLayoutRightCenter);

        vm.IsLayoutRightCenter = true;
        Assert.Equal(NotchLayout.RightCenter, vm.Layout);
        Assert.False(vm.IsLayoutTopCenter);

        vm.IsThemeDark = true;
        Assert.Equal(NotchTheme.Dark, vm.Theme);
        Assert.False(vm.IsThemeLight);
    }

    [Fact]
    public void CancelCommand_RaisesRequestClose()
    {
        var settings = CreateSettings();
        var vm = new SettingsViewModel(settings);

        bool closed = false;
        vm.RequestClose += (_, _) => closed = true;

        vm.CancelCommand.Execute(null);

        Assert.True(closed);
    }

    [Fact]
    public void NotifyResetAndSoundSettings_BindAndPersistCorrectly()
    {
        var settings = CreateSettings();
        settings.Current.PlayNotificationSound = false;
        settings.Current.Services["Claude"].NotifyOnReset = true;
        settings.Current.Services["Antigravity"].NotifyOnReset = false;

        var vm = new SettingsViewModel(settings);

        Assert.False(vm.PlayNotificationSound);
        Assert.True(vm.ClaudeNotifyReset);
        Assert.False(vm.AntigravityNotifyReset);

        vm.PlayNotificationSound = true;
        vm.ClaudeNotifyReset = false;
        vm.AntigravityNotifyReset = true;

        vm.SaveCommand.Execute(null);

        Assert.True(settings.Current.PlayNotificationSound);
        Assert.False(settings.Current.Services["Claude"].NotifyOnReset);
        Assert.True(settings.Current.Services["Antigravity"].NotifyOnReset);
    }

    [Fact]
    public void TestSoundCommand_ExecutesAndSetsStatusText()
    {
        var settings = CreateSettings();
        var vm = new SettingsViewModel(settings);

        vm.TestSoundCommand.Execute(null);

        Assert.Contains("notification chime", vm.StatusText);
    }
}

