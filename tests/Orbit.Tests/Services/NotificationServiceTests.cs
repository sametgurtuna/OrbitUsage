using System.Windows.Forms;
using Orbit.Models;
using Orbit.Services;
using Xunit;

namespace Orbit.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public void CheckAndNotify_WhenThresholdCrossed_DispatchesNotificationAndSound()
    {
        var settings = new SettingsService();
        settings.Current.PlayNotificationSound = true;
        settings.Current.Services["Claude"] = new ServiceSettings
        {
            Enabled = true,
            NotifyAt80 = true,
            LastNotifiedThreshold = 0
        };

        string? notifiedTitle = null;
        string? notifiedMsg = null;
        bool soundPlayed = false;

        var service = new NotificationService(
            settings,
            (title, msg, icon) =>
            {
                notifiedTitle = title;
                notifiedMsg = msg;
            },
            () => soundPlayed = true);

        var result = UsageResult.Ok(85.0);
        service.CheckAndNotify("Claude", result);

        Assert.NotNull(notifiedTitle);
        Assert.Contains("Claude", notifiedTitle);
        Assert.Contains("Warning", notifiedTitle);
        Assert.True(soundPlayed);
        Assert.Equal(80, settings.Current.Services["Claude"].LastNotifiedThreshold);
    }

    [Fact]
    public void CheckAndNotify_WhenQuotaResets_DispatchesResetNotificationAndSound()
    {
        var settings = new SettingsService();
        settings.Current.PlayNotificationSound = true;
        settings.Current.Services["Claude"] = new ServiceSettings
        {
            Enabled = true,
            NotifyOnReset = true,
            LastNotifiedThreshold = 95
        };

        string? notifiedTitle = null;
        string? notifiedMsg = null;
        bool soundPlayed = false;

        var service = new NotificationService(
            settings,
            (title, msg, icon) =>
            {
                notifiedTitle = title;
                notifiedMsg = msg;
            },
            () => soundPlayed = true);

        // Usage drops from 95% down to 5% (quota reset!)
        var result = UsageResult.Ok(5.0);
        service.CheckAndNotify("Claude", result);

        Assert.NotNull(notifiedTitle);
        Assert.Contains("Sıfırlandı", notifiedTitle);
        Assert.Contains("🎉", notifiedTitle);
        Assert.True(soundPlayed);
        Assert.Equal(0, settings.Current.Services["Claude"].LastNotifiedThreshold);
    }

    [Fact]
    public void CheckAndNotify_WhenNotifyOnResetDisabled_DoesNotDispatchResetNotification()
    {
        var settings = new SettingsService();
        settings.Current.PlayNotificationSound = true;
        settings.Current.Services["Claude"] = new ServiceSettings
        {
            Enabled = true,
            NotifyOnReset = false,
            LastNotifiedThreshold = 80
        };

        bool notified = false;
        bool soundPlayed = false;

        var service = new NotificationService(
            settings,
            (title, msg, icon) => notified = true,
            () => soundPlayed = true);

        var result = UsageResult.Ok(10.0);
        service.CheckAndNotify("Claude", result);

        Assert.False(notified);
        Assert.False(soundPlayed);
        Assert.Equal(0, settings.Current.Services["Claude"].LastNotifiedThreshold);
    }

    [Fact]
    public void CheckAndNotify_WhenPlaySoundDisabled_DoesNotTriggerSound()
    {
        var settings = new SettingsService();
        settings.Current.PlayNotificationSound = false;
        settings.Current.Services["Claude"] = new ServiceSettings
        {
            Enabled = true,
            NotifyOnReset = true,
            LastNotifiedThreshold = 80
        };

        bool notified = false;
        bool soundPlayed = false;

        var service = new NotificationService(
            settings,
            (title, msg, icon) => notified = true,
            () => soundPlayed = true);

        var result = UsageResult.Ok(0.0);
        service.CheckAndNotify("Claude", result);

        Assert.True(notified);
        Assert.False(soundPlayed);
    }
}
