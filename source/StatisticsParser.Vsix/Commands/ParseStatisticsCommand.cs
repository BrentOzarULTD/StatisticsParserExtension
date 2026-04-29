using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using StatisticsParser.Vsix.Windows;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix.Commands
{
    internal sealed class ParseStatisticsCommand
    {
        public static readonly Guid CommandSet = new Guid("5B21B934-FB0C-4BEC-A466-CDCB423B16E6");
        public const int CommandId = 0x0100;

        private readonly AsyncPackage _package;

        private ParseStatisticsCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
                ?? throw new InvalidOperationException("Failed to acquire IMenuCommandService.");
            _ = new ParseStatisticsCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var window = _package.FindToolWindow(typeof(StatisticsParserToolWindow), 0, true)
                ?? throw new NotSupportedException("Cannot create Statistics Parser tool window.");

            if (window.Frame is IVsWindowFrame frame)
            {
                ErrorHandler.ThrowOnFailure(frame.Show());
            }
        }
    }
}
