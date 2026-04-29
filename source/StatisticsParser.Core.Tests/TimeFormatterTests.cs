using StatisticsParser.Core.Formatting;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class TimeFormatterTests
{
    [Theory]
    [InlineData(0, "00:00:00.000")]
    [InlineData(5, "00:00:00.005")]
    [InlineData(959, "00:00:00.959")]
    [InlineData(1000, "00:00:01.000")]
    [InlineData(60000, "00:01:00.000")]
    [InlineData(3661123, "01:01:01.123")]
    public void FormatMs_ReturnsHhMmSsFff(int ms, string expected)
    {
        Assert.Equal(expected, TimeFormatter.FormatMs(ms));
    }
}
