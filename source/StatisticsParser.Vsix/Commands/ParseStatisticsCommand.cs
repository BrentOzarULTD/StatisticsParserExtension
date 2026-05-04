using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using StatisticsParser.Vsix.Capture;
using StatisticsParser.Vsix.Controls;
using StatisticsParser.Vsix.Diagnostics;
using StatisticsParser.Vsix.Windows;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix.Commands
{
    [Command(PackageGuids.guidStatisticsParserCmdSetString, PackageIds.cmdidParseStatistics)]
    internal sealed class ParseStatisticsCommand : BaseCommand<ParseStatisticsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = Package.FindToolWindow(typeof(StatisticsParserToolWindow), 0, true)
                ?? throw new NotSupportedException("Cannot create Statistics Parser tool window.");

            if (window.Frame is IVsWindowFrame frame)
                ErrorHandler.ThrowOnFailure(frame.Show());

            var control = window.Content as StatisticsParserControl;

            MessagesCaptureResult result;
            try
            {
                result = await MessagesTabReader.GetMessagesTextAsync(Package, Package.DisposalToken);
            }
            catch (Exception ex)
            {
                LogFailure("MessagesTabReader.GetMessagesTextAsync", ex);
                control?.ShowCaptureError(MessagesCaptureStatus.Failed, ex);
                return;
            }

            if (result.Status != MessagesCaptureStatus.Ok)
            {
                if (result.Error != null)
                    LogFailure("Messages capture (" + result.Status + ")", result.Error);
                control?.ShowCaptureError(result.Status, result.Error);
                return;
            }

            ParseResult parsed;
            try
            {
                parsed = Parser.ParseData(result.Text);
            }
            catch (Exception ex)
            {
                LogFailure("Parser.ParseData", ex);
                control?.ShowCaptureError(MessagesCaptureStatus.Failed, ex);
                return;
            }

            control?.ShowCapturedText(result, parsed.Data.Count);
        }

        private void LogFailure(string context, Exception ex)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var pane = StatisticsParserDiagnosticsPane.GetOrCreate(Package);
                pane.WriteFailure(context, ex);
            }
            catch
            {
                // Best-effort logging; if even the Output pane is unavailable, the on-screen error in
                // ShowCaptureError still carries the message.
            }
        }
    }
}
