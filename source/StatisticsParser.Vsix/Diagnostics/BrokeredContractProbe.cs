using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — Option 1.
    // Loads Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll, lists interfaces and
    // ServiceMoniker static members matching a name filter, attempts to acquire IServiceBroker and probe candidates.
    internal static class BrokeredContractProbe
    {
        private const string ContractsDllName = "Microsoft.SqlServer.Management.UI.VSIntegration.SqlEditor.BrokeredContracts.dll";

        private static readonly string[] NameFilter =
        {
            "Message", "Result", "Output", "Pane", "GetText", "Editor", "Script", "Query", "Execution"
        };

        public static async Task RunAsync(AsyncPackage package, ProbeOutputPane pane)
        {
            pane.WriteHeader("Probe 1: Brokered Contracts");

            string ideDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(ideDir, ContractsDllName);
            pane.WriteInfo("DLL path: " + dllPath);

            if (!File.Exists(dllPath))
            {
                pane.WriteFailure("BrokeredContracts DLL not found at expected location", new FileNotFoundException(dllPath));
                return;
            }

            Assembly asm;
            try { asm = Assembly.LoadFrom(dllPath); }
            catch (Exception ex) { pane.WriteFailure("Assembly.LoadFrom", ex); return; }

            var asmName = asm.GetName();
            pane.WriteInfo("Loaded: " + asmName.Name + " v" + asmName.Version);

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                pane.WriteFailure("GetTypes (continuing with partial type list)", ex);
                types = ex.Types?.Where(t => t != null).ToArray() ?? Array.Empty<Type>();
            }

            DumpInterfaceCandidates(types, pane);
            var monikers = DumpServiceMonikers(types, pane);
            await TryAcquireBrokerAndProbeAsync(package, asm, monikers, pane);
        }

        private static void DumpInterfaceCandidates(Type[] types, ProbeOutputPane pane)
        {
            var candidates = types
                .Where(t => t != null && t.IsInterface && NameFilter.Any(f => t.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(t => t.FullName)
                .ToList();

            pane.WriteInfo("Interface candidates matching filter: " + candidates.Count);
            foreach (var t in candidates)
            {
                pane.WriteLine("    " + t.FullName);
                foreach (var m in t.GetMethods())
                    pane.WriteLine("       " + FormatMethod(m));
                foreach (var p in t.GetProperties())
                    pane.WriteLine("       " + SafeTypeName(p.PropertyType) + " " + p.Name + " { get; }");
            }
        }

        private static List<MonikerCandidate> DumpServiceMonikers(Type[] types, ProbeOutputPane pane)
        {
            var monikers = new List<MonikerCandidate>();
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            foreach (var t in types)
            {
                if (t == null) continue;
                try
                {
                    foreach (var f in t.GetFields(bf))
                    {
                        if (!IsMonikerType(f.FieldType)) continue;
                        if (!f.IsStatic) continue;
                        try { monikers.Add(new MonikerCandidate(t, f.Name, f.GetValue(null))); }
                        catch (Exception ex) { pane.WriteFailure("Read field " + t.FullName + "." + f.Name, ex); }
                    }
                    foreach (var p in t.GetProperties(bf))
                    {
                        if (!IsMonikerType(p.PropertyType)) continue;
                        if (p.GetGetMethod(true)?.IsStatic != true) continue;
                        try { monikers.Add(new MonikerCandidate(t, p.Name, p.GetValue(null))); }
                        catch (Exception ex) { pane.WriteFailure("Read property " + t.FullName + "." + p.Name, ex); }
                    }
                }
                catch { /* swallow per-type reflection failures */ }
            }

            pane.WriteInfo("ServiceMoniker static members found: " + monikers.Count);
            foreach (var c in monikers)
                pane.WriteLine("    " + c.ContainerType.FullName + "." + c.MemberName + " = " + (c.Value?.ToString() ?? "<null>"));

            return monikers;
        }

        private static async Task TryAcquireBrokerAndProbeAsync(
            AsyncPackage package,
            Assembly contractsAsm,
            List<MonikerCandidate> monikers,
            ProbeOutputPane pane)
        {
            object broker;
            try
            {
                broker = await GetServiceBrokerAsync(package, pane);
            }
            catch (Exception ex)
            {
                pane.WriteFailure("Acquire IServiceBroker", ex);
                return;
            }
            if (broker == null) return;

            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            pane.WriteInfo("Probing each ServiceMoniker against candidate interfaces…");
            foreach (var c in monikers)
            {
                if (c.Value == null) { pane.WriteInfo(c.MemberName + ": null moniker, skipping"); continue; }
                await TryProbeMonikerAsync(broker, c, contractsAsm, pane);
            }
        }

        private static async Task<object> GetServiceBrokerAsync(AsyncPackage package, ProbeOutputPane pane)
        {
            Type sbcType = ResolveType("Microsoft.VisualStudio.Shell.ServiceBroker.SVsBrokeredServiceContainer");
            if (sbcType == null) { pane.WriteInfo("SVsBrokeredServiceContainer type not loaded; skipping broker probe."); return null; }

            var container = await package.GetServiceAsync(sbcType);
            if (container == null) { pane.WriteInfo("SVsBrokeredServiceContainer service unavailable."); return null; }

            var getBroker = container.GetType().GetMethod("GetFullAccessServiceBroker");
            if (getBroker == null) { pane.WriteInfo("GetFullAccessServiceBroker method not found on container."); return null; }

            var broker = getBroker.Invoke(container, null);
            pane.WriteSuccess("Acquired IServiceBroker: " + (broker?.GetType().FullName ?? "<null>"));
            return broker;
        }

        private static async Task TryProbeMonikerAsync(object broker, MonikerCandidate moniker, Assembly contractsAsm, ProbeOutputPane pane)
        {
            // Find a candidate interface in the same assembly whose stripped name appears in the moniker text.
            var monikerText = moniker.Value.ToString();
            var ns = moniker.ContainerType.Namespace ?? string.Empty;
            var ifaces = contractsAsm.GetTypes()
                .Where(t => t != null && t.IsInterface && (t.Namespace == ns || ns.StartsWith(t.Namespace ?? string.Empty, StringComparison.Ordinal)))
                .Where(t => monikerText.IndexOf(t.Name.TrimStart('I'), StringComparison.OrdinalIgnoreCase) >= 0
                            || monikerText.IndexOf(t.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .ToList();

            if (ifaces.Count == 0)
            {
                pane.WriteInfo("    " + moniker.MemberName + ": no obvious paired interface (try matching by hand from the dump)");
                return;
            }

            var getProxyAsync = broker.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "GetProxyAsync" && m.IsGenericMethodDefinition);
            if (getProxyAsync == null) { pane.WriteInfo("GetProxyAsync not present on broker"); return; }

            foreach (var iface in ifaces)
            {
                try
                {
                    var generic = getProxyAsync.MakeGenericMethod(iface);
                    var ps = generic.GetParameters();
                    var args = new object[ps.Length];
                    args[0] = moniker.Value;
                    if (ps.Length > 1 && ps[ps.Length - 1].ParameterType == typeof(CancellationToken))
                        args[ps.Length - 1] = CancellationToken.None;

                    var task = (Task)generic.Invoke(broker, args);
                    await task.ConfigureAwait(true);
                    var proxy = task.GetType().GetProperty("Result")?.GetValue(task);
                    if (proxy == null)
                    {
                        pane.WriteInfo("    " + moniker.MemberName + " <" + iface.Name + ">: proxy null (not registered)");
                        continue;
                    }
                    pane.WriteSuccess(moniker.MemberName + " <" + iface.Name + ">: proxy " + proxy.GetType().FullName);
                    await CallSimpleGettersAsync(proxy, iface, pane);

                    if (proxy is IDisposable d) { try { d.Dispose(); } catch { } }
                }
                catch (Exception ex)
                {
                    pane.WriteFailure(moniker.MemberName + " <" + iface.Name + ">", ex.InnerException ?? ex);
                }
            }
        }

        private static async Task CallSimpleGettersAsync(object proxy, Type iface, ProbeOutputPane pane)
        {
            foreach (var m in iface.GetMethods())
            {
                var ps = m.GetParameters();
                bool zeroArg = ps.Length == 0;
                bool ctOnly = ps.Length == 1 && ps[0].ParameterType == typeof(CancellationToken);
                if (!zeroArg && !ctOnly) continue;

                try
                {
                    var args = zeroArg ? Array.Empty<object>() : new object[] { CancellationToken.None };
                    var ret = m.Invoke(proxy, args);
                    if (ret is Task t)
                    {
                        await t.ConfigureAwait(true);
                        ret = t.GetType().GetProperty("Result")?.GetValue(t);
                    }
                    var preview = PreviewValue(ret);
                    pane.WriteLine("       " + iface.Name + "." + m.Name + "() -> " + preview);
                }
                catch (Exception ex)
                {
                    pane.WriteFailure(iface.Name + "." + m.Name + "()", ex.InnerException ?? ex);
                }
            }
        }

        private static Type ResolveType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static bool IsMonikerType(Type t)
        {
            if (t == null) return false;
            return t.Name.IndexOf("ServiceMoniker", StringComparison.Ordinal) >= 0
                || t.FullName?.IndexOf("Microsoft.ServiceHub.Framework.ServiceMoniker", StringComparison.Ordinal) >= 0
                || t.FullName?.IndexOf("Microsoft.ServiceHub.Framework.ServiceRpcDescriptor", StringComparison.Ordinal) >= 0;
        }

        private static string FormatMethod(MethodInfo m)
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => SafeTypeName(p.ParameterType) + " " + p.Name));
            return SafeTypeName(m.ReturnType) + " " + m.Name + "(" + ps + ")";
        }

        private static string SafeTypeName(Type t)
        {
            try { return t?.Name ?? "?"; } catch { return "?"; }
        }

        private static string PreviewValue(object value)
        {
            if (value == null) return "<null>";
            string s;
            try { s = value.ToString(); } catch (Exception ex) { return "<ToString threw " + ex.GetType().Name + ">"; }
            if (s == null) return "<null>";
            if (s.Length > 200) s = s.Substring(0, 200) + "…";
            return s;
        }

        private struct MonikerCandidate
        {
            public Type ContainerType;
            public string MemberName;
            public object Value;

            public MonikerCandidate(Type t, string name, object value) { ContainerType = t; MemberName = name; Value = value; }
        }
    }
}
