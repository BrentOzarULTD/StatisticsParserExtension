using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace StatisticsParser.Vsix.Diagnostics
{
    // Phase 8a discovery code — Option 2.
    // Enumerates text buffers in the Running Document Table and walks the WPF visual tree under the active
    // query window looking for any element exposing Messages-tab content.
    internal static class TextBufferProbe
    {
        public static void Run(IServiceProvider serviceProvider, ProbeOutputPane pane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            pane.WriteHeader("Probe 2: IVsTextBuffer / Visual Tree");

            try { LogActiveDocument(serviceProvider, pane); }
            catch (Exception ex) { pane.WriteFailure("LogActiveDocument", ex); }

            try { EnumerateRdt(serviceProvider, pane); }
            catch (Exception ex) { pane.WriteFailure("EnumerateRdt", ex); }

            try { WalkVisualTree(pane); }
            catch (Exception ex) { pane.WriteFailure("WalkVisualTree", ex); }
        }

        private static void LogActiveDocument(IServiceProvider sp, ProbeOutputPane pane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = sp.GetService(typeof(DTE)) as DTE;
            var doc = dte?.ActiveDocument;
            pane.WriteInfo("DTE.ActiveDocument: " + (doc == null ? "<null>" : doc.FullName + " (kind=" + doc.Kind + ", language=" + doc.Language + ")"));
        }

        private static void EnumerateRdt(IServiceProvider sp, ProbeOutputPane pane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var rdt = sp.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (rdt == null) { pane.WriteInfo("SVsRunningDocumentTable unavailable"); return; }

            int hr = rdt.GetRunningDocumentsEnum(out var enumDocs);
            if (ErrorHandler.Failed(hr) || enumDocs == null) { pane.WriteFailure("GetRunningDocumentsEnum", new InvalidOperationException("hr=" + hr)); return; }

            uint[] cookies = new uint[1];
            int total = 0, withBuffer = 0;
            while (enumDocs.Next(1, cookies, out var fetched) == 0 && fetched == 1)
            {
                total++;
                try
                {
                    hr = rdt.GetDocumentInfo(cookies[0], out _, out _, out _, out var moniker, out _, out _, out var docData);
                    if (ErrorHandler.Failed(hr)) continue;
                    pane.WriteLine("    cookie=" + cookies[0] + " moniker=" + moniker);
                    if (docData == IntPtr.Zero) continue;

                    object obj = null;
                    try { obj = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(docData); }
                    finally { System.Runtime.InteropServices.Marshal.Release(docData); }

                    var lines = obj as IVsTextLines;
                    if (lines == null && obj is IVsTextBufferProvider provider)
                    {
                        provider.GetTextBuffer(out var buf);
                        lines = buf as IVsTextLines;
                    }
                    if (lines != null)
                    {
                        withBuffer++;
                        DumpTextLinesPreview(lines, "       ", pane);
                    }
                    else
                    {
                        pane.WriteLine("       docData type: " + obj?.GetType().FullName);
                    }
                }
                catch (Exception ex) { pane.WriteFailure("RDT cookie " + cookies[0], ex); }
            }
            pane.WriteInfo("RDT entries: " + total + ", with IVsTextLines: " + withBuffer);
        }

        private static void DumpTextLinesPreview(IVsTextLines lines, string indent, ProbeOutputPane pane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int hr = lines.GetLineCount(out int count);
            if (ErrorHandler.Failed(hr)) { pane.WriteLine(indent + "GetLineCount hr=" + hr); return; }
            pane.WriteLine(indent + "lineCount=" + count);

            int previewLines = Math.Min(5, count);
            for (int i = 0; i < previewLines; i++)
            {
                if (lines.GetLengthOfLine(i, out int len) != 0) continue;
                if (lines.GetLineText(i, 0, i, len, out string text) == 0)
                {
                    if (text != null && text.Length > 200) text = text.Substring(0, 200) + "…";
                    pane.WriteLine(indent + "[" + i + "] " + text);
                }
            }
            if (count > previewLines)
            {
                pane.WriteLine(indent + "  …");
                int last = count - 1;
                if (lines.GetLengthOfLine(last, out int llen) == 0 && lines.GetLineText(last, 0, last, llen, out string ltext) == 0)
                {
                    if (ltext != null && ltext.Length > 200) ltext = ltext.Substring(0, 200) + "…";
                    pane.WriteLine(indent + "[" + last + "] " + ltext);
                }
            }
        }

        private static void WalkVisualTree(ProbeOutputPane pane)
        {
            pane.WriteInfo("Visual tree walk (active windows):");
            if (Application.Current == null) { pane.WriteInfo("Application.Current null — non-WPF host?"); return; }

            int totalElements = 0;
            int matchedElements = 0;
            foreach (System.Windows.Window window in Application.Current.Windows)
            {
                pane.WriteLine("    Window: " + window.GetType().FullName + " title=" + (window.Title ?? "<null>"));
                Walk(window, 0);
            }
            pane.WriteInfo("Visual tree walked: " + totalElements + " elements, " + matchedElements + " name/property matches.");

            void Walk(DependencyObject root, int depth)
            {
                if (root == null) return;
                if (depth > 60) return;
                totalElements++;

                try
                {
                    var typeName = root.GetType().FullName ?? "";
                    if (LooksInteresting(typeName, root))
                    {
                        matchedElements++;
                        pane.WriteLine("       [" + depth + "] " + typeName);
                        DumpDataContext(root, pane);
                    }
                }
                catch { }

                int childCount;
                try { childCount = VisualTreeHelper.GetChildrenCount(root); }
                catch { return; }

                for (int i = 0; i < childCount; i++)
                {
                    DependencyObject child = null;
                    try { child = VisualTreeHelper.GetChild(root, i); } catch { }
                    Walk(child, depth + 1);
                }
            }
        }

        private static bool LooksInteresting(string typeName, DependencyObject obj)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            if (typeName.IndexOf("Message", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (typeName.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (typeName.IndexOf("SqlScript", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (typeName.IndexOf("EditorControl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void DumpDataContext(DependencyObject obj, ProbeOutputPane pane)
        {
            try
            {
                if (!(obj is FrameworkElement fe)) return;
                var dc = fe.DataContext;
                if (dc == null) return;
                var dcType = dc.GetType().FullName;
                pane.WriteLine("              DataContext=" + dcType);
                foreach (var p in dc.GetType().GetProperties().Where(p => p.Name.IndexOf("Message", StringComparison.OrdinalIgnoreCase) >= 0
                                                                       || p.Name.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    object v = null;
                    try { v = p.GetValue(dc); } catch { continue; }
                    var preview = v == null ? "<null>" : v.ToString();
                    if (preview != null && preview.Length > 200) preview = preview.Substring(0, 200) + "…";
                    pane.WriteLine("              ." + p.Name + " (" + p.PropertyType.Name + ") = " + preview);
                }
            }
            catch { }
        }
    }
}
