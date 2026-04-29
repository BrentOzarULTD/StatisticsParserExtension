using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using StatisticsParser.Vsix.Controls;

namespace StatisticsParser.Vsix.Windows
{
    [Guid("9688C617-1219-4479-A076-D70F73065602")]
    public sealed class StatisticsParserToolWindow : ToolWindowPane
    {
        public StatisticsParserToolWindow() : base(null)
        {
            Caption = "Stats Parser";
            Content = new StatisticsParserControl();
        }
    }
}
