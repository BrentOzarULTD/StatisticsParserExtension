using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using StatisticsParser.Vsix.Capture;
using StatisticsParser.Vsix.Diagnostics;
using StatisticsParser.Vsix.InPaneTab;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix.Commands
{
    [Command(PackageGuids.guidStatisticsParserCmdSetString, PackageIds.cmdidParseStatistics)]
    internal sealed class ParseStatisticsCommand : BaseCommand<ParseStatisticsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pane = StatisticsParserDiagnosticsPane.GetOrCreate(Package);

            MessagesCaptureResult result;
            try
            {
                result = await MessagesTabReader.GetMessagesTextAsync(Package, Package.DisposalToken);
            }
            catch (Exception ex)
            {
                pane.WriteFailure("MessagesTabReader.GetMessagesTextAsync", ex);
                return;
            }

            if (result.Status != MessagesCaptureStatus.Ok)
            {
                if (result.Error != null)
                    pane.WriteFailure("Messages capture (" + result.Status + ")", result.Error);
                else
                    pane.WriteInfo("Messages capture returned status: " + result.Status);
                return;
            }

            ParseResult parsed;
            try
            {
                parsed = Parser.ParseData(result.Text);
            }
            catch (Exception ex)
            {
                pane.WriteFailure("Parser.ParseData", ex);
                return;
            }

            ResultsTabInjector.TryShow(Package, parsed, pane);
        }
    }
}
