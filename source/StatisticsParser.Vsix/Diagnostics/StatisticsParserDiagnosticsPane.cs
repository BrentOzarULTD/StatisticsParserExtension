using System;
using System.Globalization;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StatisticsParser.Vsix.Diagnostics
{
    // VS Output Window pane used for structured diagnostic traces (reflection lookups, RPC failures
    // against the SSMS brokered services, etc.). The Guid is kept stable across the Phase 8a → 8b
    // rename so users keep their docked pane.
    internal sealed class StatisticsParserDiagnosticsPane
    {
        public const string PaneTitle = "Statistics Parser — Diagnostics";
        private static readonly Guid PaneGuid = new Guid("F1E27B41-1A05-4D89-9E6F-F1E27B411A05");

        private readonly IVsOutputWindowPane _pane;

        private StatisticsParserDiagnosticsPane(IVsOutputWindowPane pane)
        {
            _pane = pane;
        }

        public static StatisticsParserDiagnosticsPane GetOrCreate(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            var output = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow))
                ?? throw new InvalidOperationException("Failed to acquire SVsOutputWindow.");

            var paneGuid = PaneGuid;
            int hr = output.GetPane(ref paneGuid, out var pane);
            if (ErrorHandler.Failed(hr) || pane == null)
            {
                ErrorHandler.ThrowOnFailure(output.CreatePane(ref paneGuid, PaneTitle, fInitVisible: 1, fClearWithSolution: 0));
                ErrorHandler.ThrowOnFailure(output.GetPane(ref paneGuid, out pane));
            }

            pane.Activate();
            return new StatisticsParserDiagnosticsPane(pane);
        }

        public void WriteHeader(string title)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WriteLine();
            WriteLine("=== " + title + " ===");
        }

        public void WriteLine(string text = "")
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane.OutputStringThreadSafe((text ?? string.Empty) + Environment.NewLine);
        }

        public void WriteSuccess(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WriteLine("  OK   " + message);
        }

        public void WriteInfo(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WriteLine("  ..   " + message);
        }

        public void WriteFailure(string context, Exception ex)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WriteLine("  FAIL " + context);
            if (ex != null)
                WriteLine("       " + ex.GetType().FullName + ": " + ex.Message);
        }

        public void WriteTimestamp(string label)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WriteLine(label + " at " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
        }
    }
}
