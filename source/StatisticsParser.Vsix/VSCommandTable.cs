using System;

namespace StatisticsParser.Vsix
{
    // Mirrors symbols declared in StatisticsParser.vsct. Keep in sync when adding/removing
    // commands, groups, or GuidSymbols. (Equivalent to the file Visual Studio's VSCT custom
    // tool would generate; we maintain it by hand because the build runs outside VS.)
    internal static partial class PackageGuids
    {
        public const string guidStatisticsParserPackageString = "0F240EE5-54A7-43CB-9710-3A8E2DEA5B46";
        public static readonly Guid guidStatisticsParserPackage = new Guid(guidStatisticsParserPackageString);

        public const string guidStatisticsParserCmdSetString = "5B21B934-FB0C-4BEC-A466-CDCB423B16E6";
        public static readonly Guid guidStatisticsParserCmdSet = new Guid(guidStatisticsParserCmdSetString);
    }

    internal static partial class PackageIds
    {
        public const int ParseStatisticsGroup = 0x1020;
        public const int cmdidParseStatistics = 0x0100;
        public const int cmdidProbeMessageSource = 0x0101;
    }
}
