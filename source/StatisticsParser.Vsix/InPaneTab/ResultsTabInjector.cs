using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using StatisticsParser.Core.Models;
using StatisticsParser.Vsix.Capture;
using StatisticsParser.Vsix.Diagnostics;

namespace StatisticsParser.Vsix.InPaneTab
{
    // Thin entry point: resolves the active SQL query window's docView, hands off to the
    // per-docView TabPageSupervisor which owns the TabPage, the WPF control, and the
    // auto-refresh event hooks.
    internal static class ResultsTabInjector
    {
        public static bool TryShow(
            AsyncPackage package,
            MessagesCaptureResult capture,
            ParseResult parsed,
            StatisticsParserDiagnosticsPane pane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (package == null) throw new ArgumentNullException(nameof(package));

            try
            {
                var docView = GetActiveDocView(package)
                    ?? throw new InvalidOperationException(
                        "No active SQL query document. Open a .sql window and run a query first.");

                var supervisor = TabPageSupervisor.GetOrCreate(docView, package, pane);
                supervisor.RenderInitial(capture, parsed);
                return true;
            }
            catch (Exception ex)
            {
                pane?.WriteFailure("ResultsTabInjector.TryShow", ex);
                return false;
            }
        }

        private static object GetActiveDocView(IServiceProvider sp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var monitor = (IVsMonitorSelection)sp.GetService(typeof(SVsShellMonitorSelection))
                ?? throw new InvalidOperationException("SVsShellMonitorSelection unavailable.");

            ErrorHandler.ThrowOnFailure(monitor.GetCurrentElementValue(
                (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frameObj));

            var frame = frameObj as IVsWindowFrame;
            if (frame == null) return null;

            ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object docView));
            return docView;
        }
    }
}
