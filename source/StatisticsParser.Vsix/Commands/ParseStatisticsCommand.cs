using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
            {
                ErrorHandler.ThrowOnFailure(frame.Show());
            }
        }
    }
}
