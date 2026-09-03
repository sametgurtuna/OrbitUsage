using Orbit.Models;
using Orbit.Services;
using Xunit;

namespace Orbit.Tests.Services;

public class AgyCliUsageScraperTests
{
    [Fact]
    public void ParseUsageJson_ValidWeeklyBucket_ExtractsUsageAndResetTime()
    {
        string json = """
        {
          "command": {
            "data": {
              "groups": [
                {
                  "name": "Gemini Models",
                  "buckets": [
                    {
                      "id": "gemini-weekly",
                      "window": "weekly",
                      "remaining_fraction": 0.40,
                      "reset_time": "2026-09-05T14:30:00Z"
                    }
                  ]
                }
              ]
            }
          }
        }
        """;

        var fixedNow = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var result = AgyCliUsageScraper.ParseUsageJson(json, nowUtc: fixedNow);

        Assert.True(result.Success);
        // (1.0 - 0.40) * 100 = 60.0%
        Assert.Equal(60.0, result.PercentUsed);
        Assert.Equal("60%", result.RawText);
        Assert.NotNull(result.ResetTimeText);
        Assert.StartsWith("in 2d", result.ResetTimeText);
    }

    [Fact]
    public void ParseUsageJson_GeminiDualBuckets_ExtractsBothWeeklyAndFiveHourLimits()
    {
        string json = """
        {
          "command": {
            "data": {
              "groups": [
                {
                  "name": "Gemini Models",
                  "buckets": [
                    {
                      "id": "gemini-weekly",
                      "name": "Weekly Limit Remaining",
                      "window": "weekly",
                      "remaining_fraction": 0.85,
                      "reset_time": "2026-09-09T10:00:00Z"
                    },
                    {
                      "id": "gemini-5h",
                      "name": "Five Hour Limit Remaining",
                      "window": "5h",
                      "remaining_fraction": 0.20,
                      "reset_time": "2026-09-03T12:30:00Z"
                    }
                  ]
                }
              ]
            }
          }
        }
        """;

        var fixedNow = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var result = AgyCliUsageScraper.ParseUsageJson(json, nowUtc: fixedNow);

        Assert.True(result.Success);
        // Weekly: (1.0 - 0.85) * 100 = 15.0%
        Assert.Equal(15.0, result.PercentUsed);
        Assert.Equal("15%", result.RawText);
        Assert.StartsWith("in 6d", result.ResetTimeText);

        // 5-hour limit: (1.0 - 0.20) * 100 = 80.0%
        Assert.True(result.HasSessionData);
        Assert.Equal(80.0, result.SessionPercentUsed);
        Assert.Equal("in 2h 30m", result.SessionResetTimeText);
    }

    [Fact]
    public void ParseUsageJson_ShortResetTime_FormatsHoursAndMinutes()
    {
        string json = """
        {
          "command": {
            "data": {
              "groups": [
                {
                  "name": "Antigravity",
                  "buckets": [
                    {
                      "id": "session-quota",
                      "remaining_fraction": 0.15,
                      "reset_time": "2026-09-03T13:45:00Z"
                    }
                  ]
                }
              ]
            }
          }
        }
        """;

        var fixedNow = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var result = AgyCliUsageScraper.ParseUsageJson(json, nowUtc: fixedNow);

        Assert.True(result.Success);
        Assert.Equal(85.0, result.PercentUsed);
        // 3 hours 45 minutes
        Assert.Equal("in 3h 45m", result.ResetTimeText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseUsageJson_EmptyOrNullString_ReturnsFailure(string? input)
    {
        var result = AgyCliUsageScraper.ParseUsageJson(input!);

        Assert.False(result.Success);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseUsageJson_MalformedJson_ReturnsFailureWithoutThrowing()
    {
        string malformed = "{ this is not valid json }";

        var result = AgyCliUsageScraper.ParseUsageJson(malformed);

        Assert.False(result.Success);
    }

    [Fact]
    public void ParseUsageJson_MissingGroupsArray_ReturnsFailure()
    {
        string json = """
        {
          "command": {
            "data": {
              "other_field": 123
            }
          }
        }
        """;

        var result = AgyCliUsageScraper.ParseUsageJson(json);

        Assert.False(result.Success);
        Assert.Contains("structure", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
