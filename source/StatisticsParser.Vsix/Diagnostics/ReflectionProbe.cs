using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — Option 3.
    // Locates SqlScriptEditorControl instances in the live process and dumps members whose name matches
    // a "messages text" filter.
    internal static class ReflectionProbe
    {
        private static readonly string[] MemberNameFilter =
        {
            "Message", "Result", "Pane", "Output", "Text"
        };

        public static void Run(ProbeOutputPane pane)
        {
            pane.WriteHeader("Probe 3: Reflection on SqlScriptEditorControl");

            try
            {
                var instances = LocateInstances(pane);
                pane.WriteInfo("SqlScriptEditorControl-like instances found: " + instances.Count);
                foreach (var inst in instances)
                {
                    try { DumpInstance(inst, pane); }
                    catch (Exception ex) { pane.WriteFailure("DumpInstance " + inst.GetType().FullName, ex); }
                }
            }
            catch (Exception ex) { pane.WriteFailure("ReflectionProbe.Run", ex); }
        }

        private static List<object> LocateInstances(ProbeOutputPane pane)
        {
            var found = new List<object>();
            var seen = new HashSet<object>(new ReferenceComparer());

            if (Application.Current == null) return found;

            foreach (Window window in Application.Current.Windows)
            {
                Walk(window);
            }

            return found;

            void Walk(DependencyObject root)
            {
                if (root == null || seen.Contains(root)) return;
                seen.Add(root);

                var typeName = root.GetType().FullName ?? "";
                if (typeName.IndexOf("SqlScriptEditorControl", StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("ScriptEditorControl", StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("MessagesPane", StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("ResultsControl", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found.Add(root);
                }

                int n;
                try { n = VisualTreeHelper.GetChildrenCount(root); } catch { return; }
                for (int i = 0; i < n; i++)
                {
                    DependencyObject child = null;
                    try { child = VisualTreeHelper.GetChild(root, i); } catch { }
                    Walk(child);
                }
            }
        }

        private static void DumpInstance(object instance, ProbeOutputPane pane)
        {
            var type = instance.GetType();
            pane.WriteLine();
            pane.WriteLine("    Instance: " + type.FullName + " (HashCode=0x" + instance.GetHashCode().ToString("X") + ")");

            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Walk type hierarchy so we hit private fields on base classes too.
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(bf))
                {
                    if (!NameMatches(f.Name)) continue;
                    DumpMember(f.FieldType, f.Name, () => f.GetValue(instance), "field", t, pane);
                }

                foreach (var p in t.GetProperties(bf))
                {
                    if (!NameMatches(p.Name)) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    var getter = p.GetGetMethod(true);
                    if (getter == null) continue;
                    DumpMember(p.PropertyType, p.Name, () => p.GetValue(instance), "prop", t, pane);
                }
            }
        }

        private static void DumpMember(Type memberType, string memberName, Func<object> read, string kind, Type declaringType, ProbeOutputPane pane)
        {
            object value;
            try { value = read(); }
            catch (Exception ex)
            {
                pane.WriteLine("       " + kind + " " + declaringType.Name + "." + memberName + " (" + memberType.Name + ") -> THREW " + ex.GetType().Name + ": " + ex.Message);
                return;
            }
            string preview;
            try { preview = value == null ? "<null>" : value.ToString(); }
            catch (Exception ex) { preview = "<ToString threw " + ex.GetType().Name + ">"; }
            if (preview != null && preview.Length > 240) preview = preview.Substring(0, 240) + "…";
            pane.WriteLine("       " + kind + " " + declaringType.Name + "." + memberName + " (" + memberType.Name + ") = " + preview);
        }

        private static bool NameMatches(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var f in MemberNameFilter)
                if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
