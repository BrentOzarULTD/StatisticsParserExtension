using System;
using System.Windows.Interop;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using StatisticsParser.Vsix.Controls;
using StatisticsParser.Vsix.Diagnostics;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix.Commands
{
    [Command(PackageGuids.guidStatisticsParserCmdSetString, PackageIds.cmdidAboutStatistics)]
    internal sealed class AboutCommand : BaseCommand<AboutCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var uiShell = await Package.GetServiceAsync<SVsUIShell, IVsUIShell>();
                IntPtr owner = IntPtr.Zero;
                if (uiShell != null)
                    ErrorHandler.ThrowOnFailure(uiShell.GetDialogOwnerHwnd(out owner));

                var dlg = new AboutDialog();
                if (owner != IntPtr.Zero)
                    new WindowInteropHelper(dlg).Owner = owner;
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                StatisticsParserDiagnosticsPane.GetOrCreate(Package).WriteFailure("AboutCommand.ExecuteAsync", ex);
            }
        }
    }
}
