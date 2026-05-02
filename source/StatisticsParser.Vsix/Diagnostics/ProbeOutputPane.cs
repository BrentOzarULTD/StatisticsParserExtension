using System;
using System.Globalization;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — remove once Phase 8b lands.
    internal sealed class ProbeOutputPane
    {
        public const string PaneTitle = "Statistics Parser — Probe";
        private static readonly Guid PaneGuid = new Guid("F1E27B41-1A05-4D89-9E6F-F1E27B411A05");

        private readonly IVsOutputWindowPane _pane;

        private ProbeOutputPane(IVsOutputWindowPane pane)
        {
            _pane = pane;
        }

        public static ProbeOutputPane GetOrCreate(IServiceProvider serviceProvider)
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
            return new ProbeOutputPane(pane);
        }

        public void WriteHeader(string title)
        {
            WriteLine();
            WriteLine("=== " + title + " ===");
        }

        public void WriteLine(string text = "")
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane.OutputStringThreadSafe((text ?? string.Empty) + Environment.NewLine);
        }

        public void WriteRaw(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (string.IsNullOrEmpty(text)) return;
            _pane.OutputStringThreadSafe(text);
        }

        public void WriteSuccess(string message)
        {
            WriteLine("  OK   " + message);
        }

        public void WriteInfo(string message)
        {
            WriteLine("  ..   " + message);
        }

        public void WriteFailure(string context, Exception ex)
        {
            WriteLine("  FAIL " + context);
            WriteLine("       " + ex.GetType().FullName + ": " + ex.Message);
            if (ex is System.Reflection.ReflectionTypeLoadException rtle && rtle.LoaderExceptions != null)
            {
                int idx = 0;
                foreach (var le in rtle.LoaderExceptions)
                {
                    if (le == null) continue;
                    WriteLine("         LoaderException[" + idx.ToString(CultureInfo.InvariantCulture) + "]: " + le.GetType().FullName + ": " + le.Message);
                    if (++idx >= 8) { WriteLine("         (more loader exceptions suppressed)"); break; }
                }
            }
        }

        public void WriteTimestamp(string label)
        {
            WriteLine(label + " at " + DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
        }
    }
}
