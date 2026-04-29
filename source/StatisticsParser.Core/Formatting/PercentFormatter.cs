using System.Globalization;

namespace StatisticsParser.Core.Formatting;

public static class PercentFormatter
{
    public static string FormatPercent(double value) =>
        value.ToString("F3", CultureInfo.InvariantCulture) + "%";
}
