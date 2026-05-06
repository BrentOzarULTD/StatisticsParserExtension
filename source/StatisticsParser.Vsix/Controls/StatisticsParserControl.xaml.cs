using System.Collections.Generic;
using System.Windows.Controls;
using StatisticsParser.Core.Models;

namespace StatisticsParser.Vsix.Controls
{
    public partial class StatisticsParserControl : UserControl
    {
        public StatisticsParserControl()
        {
            InitializeComponent();
        }

        public void Render(ParseResult parsed)
        {
            ContentPanel.Children.Clear();

            if (parsed == null || parsed.Data == null || parsed.Data.Count == 0)
            {
                ContentPanel.Children.Add(StatisticsViewBuilder.BuildEmptyState());
                return;
            }

            var pendingTime = new List<TimeRow>();

            foreach (var row in parsed.Data)
            {
                if (row is TimeRow t)
                {
                    if (!t.Summary) pendingTime.Add(t);
                    continue;
                }

                FlushTimeGroup(pendingTime);

                switch (row)
                {
                    case IoGroup g:
                        ContentPanel.Children.Add(StatisticsViewBuilder.BuildIoSection(g));
                        break;
                    case RowsAffectedRow ra:
                        ContentPanel.Children.Add(StatisticsViewBuilder.BuildRowsAffected(ra.Count));
                        break;
                    case ErrorRow er:
                        ContentPanel.Children.Add(StatisticsViewBuilder.BuildError(er.Text));
                        break;
                    case CompletionTimeRow ct:
                        ContentPanel.Children.Add(StatisticsViewBuilder.BuildCompletion(ct.Timestamp));
                        break;
                    case InfoRow ir:
                        ContentPanel.Children.Add(StatisticsViewBuilder.BuildInfo(ir.Text));
                        break;
                }
            }

            FlushTimeGroup(pendingTime);

            ContentPanel.Children.Add(StatisticsViewBuilder.BuildSectionLabel("Totals:"));
            ContentPanel.Children.Add(StatisticsViewBuilder.BuildGrandIoSection(parsed.Total.IoTotal));
            ContentPanel.Children.Add(StatisticsViewBuilder.BuildGrandTimeSection(
                parsed.Total.CompileTotal, parsed.Total.ExecutionTotal));
        }

        private void FlushTimeGroup(List<TimeRow> pending)
        {
            if (pending.Count == 0) return;
            ContentPanel.Children.Add(StatisticsViewBuilder.BuildTimeSection(pending));
            pending.Clear();
        }
    }
}
