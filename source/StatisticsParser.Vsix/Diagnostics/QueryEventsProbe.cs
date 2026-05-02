using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Shell;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — Option 4.
    // Inspects the brokered-contracts assembly for query/batch/execution event contracts. Only enumerates
    // and reports candidates; subscribing requires knowing the contract shape, which Phase 8b can pursue
    // once we see what's actually exposed.
    internal static class QueryEventsProbe
    {
        private const string ContractsDllName = "Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll";

        private static readonly string[] EventTypeFilter =
        {
            "Event", "Notification", "Listener", "Completed", "Subscriber", "Observer", "BatchExecut", "QueryExecut"
        };

        public static void Run(ProbeOutputPane pane)
        {
            pane.WriteHeader("Probe 4: Query / Execution Event Contracts");

            string ideDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(ideDir, ContractsDllName);
            if (!File.Exists(dllPath)) { pane.WriteFailure("DLL missing", new FileNotFoundException(dllPath)); return; }

            Assembly asm;
            try { asm = Assembly.LoadFrom(dllPath); }
            catch (Exception ex) { pane.WriteFailure("Assembly.LoadFrom", ex); return; }

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                pane.WriteFailure("GetTypes (partial)", ex);
                types = ex.Types?.Where(t => t != null).ToArray() ?? Array.Empty<Type>();
            }

            var eventLike = types
                .Where(t => t != null && (t.IsInterface || t.IsClass))
                .Where(t => EventTypeFilter.Any(f => t.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(t => t.FullName)
                .ToList();

            pane.WriteInfo("Event-shaped types found: " + eventLike.Count);
            foreach (var t in eventLike)
            {
                pane.WriteLine("    " + (t.IsInterface ? "interface " : "class ") + t.FullName);
                foreach (var ev in t.GetEvents())
                    pane.WriteLine("       event " + ev.EventHandlerType?.Name + " " + ev.Name);
                foreach (var m in t.GetMethods().Where(m => !m.IsSpecialName))
                {
                    var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
                    pane.WriteLine("       " + m.ReturnType.Name + " " + m.Name + "(" + ps + ")");
                }
            }

            pane.WriteInfo("Note: subscribing requires identifying a paired ServiceMoniker — see Probe 1 output for ServiceMoniker static members.");
        }
    }
}
