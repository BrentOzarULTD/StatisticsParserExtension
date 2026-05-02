using System;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using StatisticsParser.Vsix.Commands;
using StatisticsParser.Vsix.Controls;
using StatisticsParser.Vsix.Windows;
using Task = System.Threading.Tasks.Task;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — orchestrator for the four Messages-source probes plus the menu-Guid capture dump.
    [Command(PackageGuids.guidStatisticsParserCmdSetString, PackageIds.cmdidProbeMessageSource)]
    internal sealed class MessageSourceProbeCommand : BaseCommand<MessageSourceProbeCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                await RunProbesAsync();
            }
            catch (Exception ex)
            {
                await Package.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    var pane = ProbeOutputPane.GetOrCreate(Package);
                    pane.WriteFailure("MessageSourceProbeCommand.Execute (top-level)", ex);
                }
                catch { /* nothing else to do */ }
            }
        }

        private async Task RunProbesAsync()
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            var pane = ProbeOutputPane.GetOrCreate(Package);
            pane.WriteLine();
            pane.WriteLine("######################################################################");
            pane.WriteTimestamp("Probe run started");
            pane.WriteLine("######################################################################");

            ShowToolWindow();

            try { await BrokeredContractProbe.RunAsync(Package, pane); }
            catch (Exception ex) { pane.WriteFailure("BrokeredContractProbe.RunAsync", ex); }

            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            try { TextBufferProbe.Run(Package, pane); }
            catch (Exception ex) { pane.WriteFailure("TextBufferProbe.Run", ex); }

            try { ReflectionProbe.Run(pane); }
            catch (Exception ex) { pane.WriteFailure("ReflectionProbe.Run", ex); }

            try { QueryEventsProbe.Run(pane); }
            catch (Exception ex) { pane.WriteFailure("QueryEventsProbe.Run", ex); }

            try { MenuGuidCapture.Instance?.Dump(pane, reset: true); }
            catch (Exception ex) { pane.WriteFailure("MenuGuidCapture.Dump", ex); }

            pane.WriteLine();
            pane.WriteTimestamp("Probe run completed");
        }

        private void ShowToolWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = Package.FindToolWindow(typeof(StatisticsParserToolWindow), 0, true);
            if (window?.Frame is IVsWindowFrame frame)
            {
                ErrorHandler.ThrowOnFailure(frame.Show());
                if (window.Content is StatisticsParserControl control)
                    control.ShowProbeRanMessage(ProbeOutputPane.PaneTitle);
            }
        }
    }
}
