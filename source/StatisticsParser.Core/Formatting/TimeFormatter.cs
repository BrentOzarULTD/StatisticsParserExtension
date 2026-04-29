using System;
using System.Globalization;

namespace StatisticsParser.Core.Formatting;

public static class TimeFormatter
{
    public static string FormatMs(int ms) =>
        TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
