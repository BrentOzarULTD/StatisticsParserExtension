using System.ComponentModel;
using System.Runtime.InteropServices;
using Community.VisualStudio.Toolkit;

namespace StatisticsParser.Vsix.Options
{
    public enum TempTableNameMode
    {
        [Description("Do not change names")]
        DoNotChange = 0,

        [Description("Shorten names")]
        Shorten = 1,
    }

    [ComVisible(true)]
    [Guid("4F2A1B86-9E1C-4D3A-8C2B-7E4D5A6F3C21")]
    public class StatisticsParserOptions : BaseOptionModel<StatisticsParserOptions>
    {
        public bool ConvertCompletionTimeToLocalTime { get; set; } = true;

        public TempTableNameMode TempTableNames { get; set; } = TempTableNameMode.Shorten;
    }
}
