using System.Windows.Controls;

namespace StatisticsParser.Vsix.Controls
{
    public partial class StatisticsParserControl : UserControl
    {
        public StatisticsParserControl()
        {
            InitializeComponent();
        }

        // Phase 8a: confirm to the user that the discovery probe ran and where to read results.
        public void ShowProbeRanMessage(string outputPaneTitle)
        {
            EmptyStateText.Text = "Probe ran — open View > Output and switch to pane: \"" + outputPaneTitle + "\".";
        }
    }
}
