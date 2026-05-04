using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — passive capture of every (cmdGroup, cmdId) seen via QueryStatus while right-clicking
    // around the Messages tab. Registers as a priority command target so it sees every command in the IDE.
    // De-duplicates so the dump stays manageable; flag-counts each unique pair so the user can diff "before" vs "after"
    // by triggering known menus and looking at counts.
    internal sealed class MenuGuidCapture : IOleCommandTarget
    {
        private static readonly object _gate = new object();
        private static MenuGuidCapture _instance;
        private static uint _cookie;

        private readonly Dictionary<(Guid CmdGroup, uint CmdId), int> _seen = new Dictionary<(Guid, uint), int>();

        private MenuGuidCapture() { }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            lock (_gate)
            {
                if (_instance != null) return;
                _instance = new MenuGuidCapture();
            }

            var registerService = await package.GetServiceAsync(typeof(SVsRegisterPriorityCommandTarget)) as IVsRegisterPriorityCommandTarget;
            if (registerService == null) return;

            ErrorHandler.ThrowOnFailure(registerService.RegisterPriorityCommandTarget(0, _instance, out _cookie));
        }

        public static MenuGuidCapture Instance
        {
            get { lock (_gate) { return _instance; } }
        }

        public void Dump(StatisticsParserDiagnosticsPane pane, bool reset)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            pane.WriteHeader("Menu-Guid Capture (passive, deduped)");
            KeyValuePair<(Guid, uint), int>[] snapshot;
            lock (_gate)
            {
                snapshot = new KeyValuePair<(Guid, uint), int>[_seen.Count];
                int idx = 0;
                foreach (var kv in _seen) snapshot[idx++] = kv;
                if (reset) _seen.Clear();
            }
            pane.WriteInfo("Unique (cmdGroup, cmdId) pairs seen since last reset: " + snapshot.Length);
            Array.Sort(snapshot, (a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in snapshot)
            {
                pane.WriteLine("    {" + kv.Key.Item1 + "} 0x" + kv.Key.Item2.ToString("X4") + " — fired " + kv.Value + " time(s)");
            }
            if (reset) pane.WriteInfo("Capture state reset; right-click around the Messages tab and run probe again to capture fresh pairs.");
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (cCmds > 0 && prgCmds != null)
            {
                var key = (pguidCmdGroup, prgCmds[0].cmdID);
                lock (_gate)
                {
                    if (_seen.TryGetValue(key, out int count)) _seen[key] = count + 1;
                    else _seen[key] = 1;
                }
            }
            // Always defer; we only observe.
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }
    }
}
