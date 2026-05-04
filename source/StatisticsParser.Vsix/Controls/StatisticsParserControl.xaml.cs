using System;
using System.Windows;
using System.Windows.Controls;
using StatisticsParser.Vsix.Capture;

namespace StatisticsParser.Vsix.Controls
{
    public partial class StatisticsParserControl : UserControl
    {
        public StatisticsParserControl()
        {
            InitializeComponent();
        }

        // Phase 8b: minimum-viable display. Phase 9 replaces this with Render(ParseResult) and
        // structured DataGrids. Both ShowCapturedText and ShowCaptureError are throwaway.
        public void ShowCapturedText(MessagesCaptureResult result, int parsedRowCount)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            CapturedTextPanel.Visibility = Visibility.Visible;
            int charCount = result.Text?.Length ?? 0;
            StatusText.Text = "Captured " + charCount + " char" + (charCount == 1 ? string.Empty : "s") +
                              " from Messages tab; parsed " + parsedRowCount +
                              " row" + (parsedRowCount == 1 ? string.Empty : "s") + ".";
            CapturedText.Text = result.Text ?? string.Empty;
        }

        public void ShowCaptureError(MessagesCaptureStatus status, Exception error)
        {
            CapturedTextPanel.Visibility = Visibility.Collapsed;
            EmptyStateText.Visibility = Visibility.Visible;
            EmptyStateText.Text = FormatErrorMessage(status, error);
        }

        private static string FormatErrorMessage(MessagesCaptureStatus status, Exception error)
        {
            switch (status)
            {
                case MessagesCaptureStatus.NoActiveWindow:
                    return "No active SQL query window. Open a .sql file, run a query, then try again.";
                case MessagesCaptureStatus.EmptyMessages:
                    return "Messages tab is empty. Enable SET STATISTICS IO, TIME ON and run a query first.";
                case MessagesCaptureStatus.ContractsAssemblyMissing:
                    return "SSMS BrokeredContracts assembly not found. " +
                           "This extension targets SSMS 22; verify the installation.";
                case MessagesCaptureStatus.ProxyUnavailable:
                    return "Could not reach the SSMS query-editor service. " +
                           (error?.Message ?? "Try restarting SSMS.");
                case MessagesCaptureStatus.Failed:
                    return "Could not read the Messages tab: " + (error?.Message ?? "unknown error");
                default:
                    return "Unexpected capture state: " + status;
            }
        }
    }
}
