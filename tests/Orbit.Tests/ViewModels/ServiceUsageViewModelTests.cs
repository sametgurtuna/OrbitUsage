using Orbit.ViewModels;
using Xunit;

namespace Orbit.Tests.ViewModels;

public class ServiceUsageViewModelTests
{
    [Fact]
    public void DefaultQuotaMode_IsWeekly()
    {
        var vm = new ServiceUsageViewModel("Claude", "Claude", "#D97706")
        {
            HasSessionGauge = true,
            SessionPercentUsed = 28.0,
            PercentUsed = 50.0
        };

        // Default is Weekly mode - shows weekly usage
        Assert.Equal(QuotaWindowMode.Weekly, vm.ActiveQuotaMode);
        Assert.Equal(50.0, vm.ActivePercentRemaining); // 100 - 50% weekly used
        Assert.Equal("50%", vm.ActivePercentDisplay);
        Assert.Equal("Weekly", vm.ActiveWindowDisplay);
    }

    [Fact]
    public void ToggleQuotaMode_SwitchesFromWeeklyToFiveHour()
    {
        var vm = new ServiceUsageViewModel("Claude", "Claude", "#D97757")
        {
            HasSessionGauge = true,
            SessionPercentUsed = 40.0,
            PercentUsed = 13.0
        };

        // Default: Weekly
        Assert.Equal(QuotaWindowMode.Weekly, vm.ActiveQuotaMode);
        Assert.Equal(87.0, vm.ActivePercentRemaining); // 100 - 13%
        Assert.Equal("Weekly", vm.ActiveWindowDisplay);

        // Toggle to 5h (session)
        vm.ToggleQuotaMode();
        Assert.Equal(QuotaWindowMode.FiveHour, vm.ActiveQuotaMode);
        Assert.Equal(60.0, vm.ActivePercentRemaining); // 100 - 40%
        Assert.Equal("5h", vm.ActiveWindowDisplay);

        // Toggle back to Weekly
        vm.ToggleQuotaMode();
        Assert.Equal(QuotaWindowMode.Weekly, vm.ActiveQuotaMode);
        Assert.Equal(87.0, vm.ActivePercentRemaining);
        Assert.Equal("Weekly", vm.ActiveWindowDisplay);
    }

    [Fact]
    public void ToggleQuotaMode_NoOpWhenNoSessionGauge()
    {
        var vm = new ServiceUsageViewModel("ChatGpt", "ChatGPT", "#10A37F")
        {
            HasSessionGauge = false,
            PercentUsed = 30.0
        };

        Assert.Equal(QuotaWindowMode.Weekly, vm.ActiveQuotaMode);
        vm.ToggleQuotaMode(); // should be no-op
        Assert.Equal(QuotaWindowMode.Weekly, vm.ActiveQuotaMode);
        Assert.Equal("Weekly", vm.ActiveWindowDisplay);
    }

    [Fact]
    public void ActiveStatusDisplay_PacesCorrectly()
    {
        var vm = new ServiceUsageViewModel("Claude", "Claude", "#D97757")
        {
            HasSessionGauge = true,
            PercentUsed = 30.0 // Weekly: 70% remaining -> Plenty
        };

        Assert.Equal("Plenty", vm.ActiveStatusDisplay);

        vm.PercentUsed = 55.0; // Weekly: 45% remaining -> On pace
        Assert.Equal("On pace", vm.ActiveStatusDisplay);

        vm.PercentUsed = 85.0; // Weekly: 15% remaining with reset time
        vm.ResetTimeText = "Sun 2:00 AM";
        Assert.Equal("Reset Sun 2:00 AM", vm.ActiveStatusDisplay);

        vm.ResetTimeText = null; // no reset time -> Low
        Assert.Equal("Low", vm.ActiveStatusDisplay);
    }

    [Fact]
    public void FlyoutProperties_Claude_FormatsCorrectly()
    {
        var vm = new ServiceUsageViewModel("Claude", "Claude", "#FF5722")
        {
            HasSessionGauge = true,
            SessionPercentUsed = 73.0,
            SessionResetTimeText = "in 51 min",
            PercentUsed = 7.0,
            ResetTimeText = "Thu 12:00 AM"
        };

        Assert.Equal("Current session", vm.TopLimitTitle);
        Assert.Equal(73.0, vm.TopLimitPercent);
        Assert.Equal("73%", vm.TopLimitPercentDisplay);
        Assert.Equal("73% Used", vm.TopLimitUsedDisplay);
        Assert.Equal("Resets in 51 min", vm.TopLimitResetDisplay);

        Assert.Equal("All models", vm.BottomLimitTitle);
        Assert.Equal(7.0, vm.BottomLimitPercent);
        Assert.Equal("7%", vm.BottomLimitPercentDisplay);
        Assert.Equal("7% Used", vm.BottomLimitUsedDisplay);
        Assert.Equal("Resets Thu 12:00 AM", vm.BottomLimitResetDisplay);

        Assert.Equal(73.0, vm.DockPercentUsed);
        Assert.Equal("73%", vm.DockPercentDisplay);
    }

    [Fact]
    public void FlyoutProperties_Antigravity_FormatsCorrectly()
    {
        var vm = new ServiceUsageViewModel("Antigravity", "Antigravity", "#38BDF8")
        {
            HasSessionGauge = true,
            SessionPercentUsed = 52.0,
            SessionResetTimeText = "in 2h 15m",
            PercentUsed = 21.0,
            ResetTimeText = "in 3d 12h"
        };

        Assert.Equal("5 hour limit", vm.TopLimitTitle);
        Assert.Equal(52.0, vm.TopLimitPercent);
        Assert.Equal("52%", vm.TopLimitPercentDisplay);
        Assert.Equal("52% Used", vm.TopLimitUsedDisplay);
        Assert.Equal("Resets in 2h 15m", vm.TopLimitResetDisplay);

        Assert.Equal("Weekly limit", vm.BottomLimitTitle);
        Assert.Equal(21.0, vm.BottomLimitPercent);
        Assert.Equal("21%", vm.BottomLimitPercentDisplay);
        Assert.Equal("21% Used", vm.BottomLimitUsedDisplay);
        Assert.Equal("Resets in 3d 12h", vm.BottomLimitResetDisplay);

        Assert.Equal(52.0, vm.DockPercentUsed);
        Assert.Equal("52%", vm.DockPercentDisplay);
    }

    [Fact]
    public void NotchViewModel_ServiceOrder_ClaudeFirst_AntigravitySecond()
    {
        var notchVm = new NotchViewModel();
        Assert.True(notchVm.Services.Count >= 2);
        Assert.Equal("Claude", notchVm.Services[0].ServiceKey);
        Assert.Equal("Antigravity", notchVm.Services[1].ServiceKey);

        // Claude matches visual: 73% session, 7% weekly
        var claude = notchVm.Services[0];
        Assert.Equal(73.0, claude.TopLimitPercent);
        Assert.Equal(7.0, claude.BottomLimitPercent);

        // Antigravity matches visual: 52% 5h, 21% weekly
        var agy = notchVm.Services[1];
        Assert.Equal(52.0, agy.TopLimitPercent);
        Assert.Equal(21.0, agy.BottomLimitPercent);
    }
}
