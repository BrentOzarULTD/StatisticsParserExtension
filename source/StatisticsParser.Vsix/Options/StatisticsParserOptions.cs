using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace StatisticsParser.Vsix.Options
{
    public enum TempTableNameMode
    {
        [Description("Do not change names")]
        DoNotChange = 0,

        [Description("Shorten names")]
        Shorten = 1,
    }

    // Cache populated from the Unified Settings store. The earlier BaseOptionModel<T> approach
    // wrote to Microsoft.VisualStudio.Settings.SettingsStore — a different store from the one
    // SSMS's Tools > Options page binds to via registration.json — so user changes never reached
    // the read-sites here regardless of the migration mode. Reading directly from ISettingsReader
    // is the canonical SSMS 22 / VS 2026 pattern (cf. Hadr.registration.json, which also has no
    // migration block).
    public static class StatisticsParserOptions
    {
        public const string ConvertCompletionTimeToLocalTimeMoniker = "statisticsParser.convertCompletionTimeToLocalTime";
        public const string TempTableNamesMoniker = "statisticsParser.tempTableNamesMode";

        // Initial values match registration.json defaults so reads before the first Refresh()
        // (e.g. a render that races package init) still produce sensible output.
        public static bool ConvertCompletionTimeToLocalTime;
        public static TempTableNameMode TempTableNames;

        // GetValueOrThrow returns the registration.json default when no user value is persisted,
        // and only throws on schema mismatch / unregistered moniker — both of which are bugs in
        // this code, not runtime conditions.
        public static void Refresh(ISettingsReader reader)
        {
            ConvertCompletionTimeToLocalTime =
                reader.GetValueOrThrow<bool>(ConvertCompletionTimeToLocalTimeMoniker);

            var raw = reader.GetValueOrThrow<string>(TempTableNamesMoniker);
            TempTableNames = string.Equals(raw, "shorten", StringComparison.OrdinalIgnoreCase)
                ? TempTableNameMode.Shorten
                : TempTableNameMode.DoNotChange;
        }
    }
}
