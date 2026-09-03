using Orbit.Helpers;
using Orbit.Models;
using Xunit;

namespace Orbit.Tests.Helpers;

public class ScreenPositionHelperTests
{
    [Fact]
    public void CalculateWindowBounds_TopCenter_CentersHorizontallyAndAnchorsTop()
    {
        // 1920x1080 screen, 100% DPI (dpiScale = 1.0)
        double screenLeft = 0;
        double screenTop = 0;
        double screenWidth = 1920;
        double screenHeight = 1080;

        var bounds = ScreenPositionHelper.CalculateWindowBounds(
            NotchLayout.TopCenter,
            screenLeft,
            screenTop,
            screenWidth,
            screenHeight,
            dpiScale: 1.0,
            offsetX: 0,
            offsetY: 0);

        Assert.Equal(ScreenPositionHelper.TopCenterWindowWidth, bounds.Width);
        Assert.Equal(ScreenPositionHelper.TopCenterWindowHeight, bounds.Height);
        // Centered: (1920 - 600) / 2 = 660
        Assert.Equal(660, bounds.Left);
        Assert.Equal(0, bounds.Top);
    }

    [Fact]
    public void CalculateWindowBounds_TopCenter_WithOffsets_AppliesOffsetsCorrectly()
    {
        var bounds = ScreenPositionHelper.CalculateWindowBounds(
            NotchLayout.TopCenter,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaWidth: 1920,
            workAreaHeight: 1080,
            dpiScale: 1.0,
            offsetX: 25,
            offsetY: 15);

        // 660 + 25 = 685
        Assert.Equal(685, bounds.Left);
        Assert.Equal(15, bounds.Top);
    }

    [Fact]
    public void CalculateWindowBounds_TopCenter_HighDpi_ScalesCoordinatesAccurately()
    {
        // 3840x2160 screen at 150% DPI (1.5)
        var bounds = ScreenPositionHelper.CalculateWindowBounds(
            NotchLayout.TopCenter,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaWidth: 3840,
            workAreaHeight: 2160,
            dpiScale: 1.5,
            offsetX: 0,
            offsetY: 0);

        // Scaled width = 3840 / 1.5 = 2560. Centered: (2560 - 600) / 2 = 980
        Assert.Equal(980, bounds.Left);
        Assert.Equal(0, bounds.Top);
        Assert.Equal(600, bounds.Width);
        Assert.Equal(320, bounds.Height);
    }

    [Fact]
    public void CalculateWindowBounds_RightCenter_AnchorsToRightAndCentersVertically()
    {
        // 1920x1080 screen, 100% DPI
        var bounds = ScreenPositionHelper.CalculateWindowBounds(
            NotchLayout.RightCenter,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaWidth: 1920,
            workAreaHeight: 1080,
            dpiScale: 1.0,
            offsetX: 0,
            offsetY: 0);

        Assert.Equal(ScreenPositionHelper.RightCenterWindowWidth, bounds.Width); // 380
        Assert.Equal(ScreenPositionHelper.RightCenterWindowHeight, bounds.Height); // 520
        // Right anchor flush against edge: 1920 - 380 = 1540
        Assert.Equal(1540, bounds.Left);
        // Vertical center: (1080 - 520) / 2 = 280
        Assert.Equal(280, bounds.Top);
    }

    [Fact]
    public void CalculateWindowBounds_RightCenter_WithOffsets_AdjustsPosition()
    {
        var bounds = ScreenPositionHelper.CalculateWindowBounds(
            NotchLayout.RightCenter,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaWidth: 1920,
            workAreaHeight: 1080,
            dpiScale: 1.0,
            offsetX: -10,
            offsetY: 20);

        Assert.Equal(1530, bounds.Left); // 1540 - 10
        Assert.Equal(300, bounds.Top);   // 280 + 20
    }
}
