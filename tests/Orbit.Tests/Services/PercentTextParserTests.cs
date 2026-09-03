using Orbit.Services;
using Xunit;

namespace Orbit.Tests.Services;

public class PercentTextParserTests
{
    [Theory]
    [InlineData("Current usage: 42%", 42.0)]
    [InlineData("85.5% used", 85.5)]
    [InlineData("99,4 % left", 99.4)]
    [InlineData("0%", 0.0)]
    [InlineData("100%", 100.0)]
    public void ExtractPercent_WithDefaultPattern_ExtractsCorrectValue(string text, double expected)
    {
        double? result = PercentTextParser.ExtractPercent(text, pattern: "");

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value, precision: 1);
    }

    [Fact]
    public void ExtractPercent_WithInvert_SubtractsFromHundred()
    {
        string text = "25% remaining";
        double? result = PercentTextParser.ExtractPercent(text, pattern: "", invert: true);

        Assert.NotNull(result);
        Assert.Equal(75.0, result.Value, precision: 1);
    }

    [Theory]
    [InlineData("150%", 100.0)]
    [InlineData("-20%", 0.0)]
    public void ExtractPercent_ClampsValuesToValidRange(string text, double expected)
    {
        double? result = PercentTextParser.ExtractPercent(text, @"(-?\d+)\s*%", invert: false);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void ExtractPercent_WithCustomRegexPattern_MatchesCorrectGroup()
    {
        string text = "Quota used: [78/100]";
        string customPattern = @"\[(\d+)/100\]";

        double? result = PercentTextParser.ExtractPercent(text, customPattern);

        Assert.NotNull(result);
        Assert.Equal(78.0, result.Value);
    }

    [Fact]
    public void ExtractPercent_WithNonMatchingText_ReturnsNull()
    {
        string text = "No numbers or percentage here";
        double? result = PercentTextParser.ExtractPercent(text, pattern: "");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractPercent_WithMalformedRegex_ReturnsNullSafely()
    {
        string text = "50%";
        string invalidRegex = "[unclosed regex";

        double? result = PercentTextParser.ExtractPercent(text, invalidRegex);

        Assert.Null(result);
    }
}
