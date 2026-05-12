using System.Text.RegularExpressions;

namespace StatisticsParser.Core.Formatting;

// Display formatter for SQL Server table names. SQL Server-generated temp tables look like
// "#Orders______...______000000000157" — a long underscore run hides the meaningful suffix
// and pushes other grid columns off-screen. We only collapse runs in tables prefixed with
// '#' (temp tables); regular tables are left untouched. Threshold of 7 ensures the
// replacement (5 chars) is always strictly shorter than the original.
public static class TableNameFormatter
{
    private const string Replacement = "__…__";
    private static readonly Regex LongRun = new Regex(@"_{7,}", RegexOptions.Compiled);

    // SQL Server pads session-temp names to sysname (128 chars) with underscores and a
    // 12-hex-character suffix (e.g. "_____0000000001DC"). Stripping that signature recovers
    // the original user-written name. Requiring exactly 12 trailing hex chars avoids trimming
    // real tables that happen to end in `_<short-hex>`.
    private static readonly Regex GeneratedSuffix = new Regex(@"_+[0-9A-Fa-f]{12}$", RegexOptions.Compiled);

    public static string FormatForDisplay(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] != '#') return name;
        return LongRun.Replace(name, Replacement);
    }

    public static bool IsTruncated(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] != '#') return false;
        return LongRun.IsMatch(name);
    }

    public static string StripGeneratedSuffix(string name)
    {
        if (!IsSessionTemp(name)) return name;
        return GeneratedSuffix.Replace(name, "");
    }

    public static bool HasGeneratedSuffix(string name)
    {
        if (!IsSessionTemp(name)) return false;
        return GeneratedSuffix.IsMatch(name);
    }

    private static bool IsSessionTemp(string name)
        => !string.IsNullOrEmpty(name)
           && name[0] == '#'
           && (name.Length == 1 || name[1] != '#');
}
