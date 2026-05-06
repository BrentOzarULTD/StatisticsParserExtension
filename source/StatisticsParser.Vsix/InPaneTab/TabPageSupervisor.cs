using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Microsoft.VisualStudio.Shell;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using StatisticsParser.Vsix.Capture;
using StatisticsParser.Vsix.Controls;
using StatisticsParser.Vsix.Diagnostics;

namespace StatisticsParser.Vsix.InPaneTab
{
    // One supervisor per SqlScriptEditorControl (i.e., per SSMS query window). Owns the WPF
    // StatisticsParserControl, manages our TabPage's lifecycle, hooks SSMS query-completion
    // events, and on each completion: re-creates the tab if SSMS removed it (it does, on query
    // start), refreshes the WPF control with the latest Messages text, and conditionally
    // restores selection if the user had Parse Statistics active.
    //
    // Stored in a ConditionalWeakTable keyed on docView so the supervisor (and its event
    // subscriptions) get collected together with docView when the query window closes — no
    // explicit unhook needed.
    internal sealed class TabPageSupervisor
    {
        private const string TabPageName = "StatsParserParseStatisticsTab";
        private const string TabHeaderText = "Parse Statistics";

        private static readonly ConditionalWeakTable<object, TabPageSupervisor> _supervisors
            = new ConditionalWeakTable<object, TabPageSupervisor>();

        // Heuristic event-name patterns. Hooked events are still logged by name to the diagnostics
        // pane so we can refine if we miss the right one or hook too aggressively.
        private static readonly string[] _eventNamePatterns =
        {
            "Completed", "Executed", "Finished", "Stopped", "Done"
        };

        private readonly object _docView;
        private readonly AsyncPackage _package;
        private readonly StatisticsParserDiagnosticsPane _pane;

        private TabPage _tabPage;
        private StatisticsParserControl _wpfControl;
        private bool _eventsHooked;

        public static TabPageSupervisor GetOrCreate(object docView, AsyncPackage package, StatisticsParserDiagnosticsPane pane)
        {
            return _supervisors.GetValue(docView, dv => new TabPageSupervisor(dv, package, pane));
        }

        private TabPageSupervisor(object docView, AsyncPackage package, StatisticsParserDiagnosticsPane pane)
        {
            _docView = docView;
            _package = package;
            _pane = pane;
        }

        // First-invocation render: ensures the tab exists, fills in the parsed content, selects
        // the new tab (the user just asked for it), and hooks completion events for auto-refresh.
        public void RenderInitial(MessagesCaptureResult capture, ParseResult parsed)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var tabControl = ResolveTabControl();
            EnsureTabExists(tabControl);
            _wpfControl.ShowCapturedText(capture, parsed.Data.Count);
            tabControl.SelectedTab = _tabPage;

            if (!_eventsHooked)
            {
                HookQueryCompletionEvents();
                _eventsHooked = true;
            }
        }

        private TabControl ResolveTabControl()
        {
            // Re-resolve every render in case SSMS replaces the TabPageHost between query runs.
            var hostObj = GetTabPageHost(_docView)
                ?? throw new InvalidOperationException("SqlScriptEditorControl.TabPageHost is null.");

            return AsTabControl(hostObj)
                ?? throw new InvalidOperationException(
                    "TabPageHost is neither a TabControl nor a container of one: " + hostObj.GetType().FullName);
        }

        // SqlScriptEditorControl.TabPageHost (public property) — verified by the Stage-1 probe.
        // Falls back to the private 'tabPagesHost' field if the property is missing in a future
        // SSMS minor version.
        private static object GetTabPageHost(object docView)
        {
            var t = docView.GetType();

            var prop = t.GetProperty("TabPageHost",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanRead) return prop.GetValue(docView);

            var field = FindField(t, "tabPagesHost") ?? FindField(t, "m_tabPagesHost");
            if (field != null) return field.GetValue(docView);

            throw new InvalidOperationException(
                "Could not find TabPageHost property or tabPagesHost field on " + t.FullName);
        }

        private static TabControl AsTabControl(object obj)
        {
            if (obj is TabControl direct) return direct;
            if (obj is Control container) return FindFirstTabControl(container);
            return null;
        }

        private static TabControl FindFirstTabControl(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is TabControl tc) return tc;
                var deep = FindFirstTabControl(child);
                if (deep != null) return deep;
            }
            return null;
        }

        private void EnsureTabExists(TabControl tabControl)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_tabPage != null && tabControl.TabPages.Contains(_tabPage)) return;

            _wpfControl = new StatisticsParserControl();
            var elementHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _wpfControl,
            };
            _tabPage = new TabPage(TabHeaderText) { Name = TabPageName };
            _tabPage.Controls.Add(elementHost);
            tabControl.TabPages.Add(_tabPage);
        }

        private void HookQueryCompletionEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                int hooked = HookEventsOn(_docView);

                var resultsControlField = FindField(_docView.GetType(), "m_sqlResultsControl");
                var resultsControl = resultsControlField?.GetValue(_docView);
                if (resultsControl != null)
                    hooked += HookEventsOn(resultsControl);

                if (hooked == 0)
                    _pane?.WriteFailure(
                        "Auto-refresh disabled: no query-completion events matched the name heuristic",
                        new InvalidOperationException(
                            "Searched docView and m_sqlResultsControl for events containing 'Completed', " +
                            "'Executed', 'Finished', 'Stopped', or 'Done'."));
            }
            catch (Exception ex)
            {
                _pane?.WriteFailure("HookQueryCompletionEvents", ex);
            }
        }

        private int HookEventsOn(object instance)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int hooked = 0;
            for (var t = instance.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                EventInfo[] events;
                try
                {
                    events = t.GetEvents(BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch { continue; }

                foreach (var evt in events)
                {
                    if (evt.EventHandlerType == null) continue;
                    bool match = _eventNamePatterns.Any(p =>
                        evt.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) continue;

                    try
                    {
                        var handler = BuildHandler(evt.EventHandlerType);
                        evt.AddEventHandler(instance, handler);
                        hooked++;
                    }
                    catch (Exception ex)
                    {
                        _pane?.WriteFailure("AddEventHandler " + t.Name + "." + evt.Name, ex);
                    }
                }
            }
            return hooked;
        }

        // Builds a delegate of the event's actual delegate type whose body forwards (sender, args)
        // to OnQueryCompletionEvent. Uses Expression.Lambda so we can adapt to arbitrary delegate
        // signatures (EventHandler, EventHandler<T>, custom delegate types) without compile-time
        // knowledge.
        private Delegate BuildHandler(Type delegateType)
        {
            var invokeMethod = delegateType.GetMethod("Invoke")
                ?? throw new InvalidOperationException("Delegate type has no Invoke method.");
            var parameters = invokeMethod.GetParameters();
            if (parameters.Length != 2)
                throw new InvalidOperationException("Unsupported event signature (need 2 parameters).");

            var senderParam = Expression.Parameter(parameters[0].ParameterType, "sender");
            var argsParam = Expression.Parameter(parameters[1].ParameterType, "args");

            var thisConst = Expression.Constant(this);
            var handlerMethod = typeof(TabPageSupervisor).GetMethod(
                nameof(OnQueryCompletionEvent), BindingFlags.NonPublic | BindingFlags.Instance);

            var senderObj = parameters[0].ParameterType == typeof(object)
                ? (Expression)senderParam
                : Expression.Convert(senderParam, typeof(object));

            var argsObj = typeof(EventArgs).IsAssignableFrom(parameters[1].ParameterType)
                ? (parameters[1].ParameterType == typeof(EventArgs)
                    ? (Expression)argsParam
                    : Expression.Convert(argsParam, typeof(EventArgs)))
                : Expression.Constant(EventArgs.Empty, typeof(EventArgs));

            var callHandler = Expression.Call(thisConst, handlerMethod, senderObj, argsObj);
            return Expression.Lambda(delegateType, callHandler, senderParam, argsParam).Compile();
        }

        private void OnQueryCompletionEvent(object sender, EventArgs e)
        {
            // Events may fire from background threads; hop to UI thread for all SSMS work.
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                    await RefreshAsync();
                }
                catch (Exception ex)
                {
                    try
                    {
                        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                        _pane?.WriteFailure("OnQueryCompletionEvent", ex);
                    }
                    catch { /* best-effort logging */ }
                }
            });
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            TabControl tabControl;
            try { tabControl = ResolveTabControl(); }
            catch (Exception ex)
            {
                _pane?.WriteFailure("RefreshAsync.ResolveTabControl", ex);
                return;
            }

            MessagesCaptureResult capture;
            try
            {
                capture = await MessagesTabReader.GetMessagesTextAsync(_package, _package.DisposalToken);
            }
            catch (Exception ex)
            {
                _pane?.WriteFailure("RefreshAsync.MessagesTabReader", ex);
                return;
            }

            if (capture.Status != MessagesCaptureStatus.Ok)
            {
                // Quiet: empty Messages on a non-statistics query is normal during auto-refresh.
                if (capture.Status != MessagesCaptureStatus.EmptyMessages && capture.Error != null)
                    _pane?.WriteFailure("Auto-refresh capture (" + capture.Status + ")", capture.Error);
                return;
            }

            ParseResult parsed;
            try { parsed = Parser.ParseData(capture.Text); }
            catch (Exception ex)
            {
                _pane?.WriteFailure("RefreshAsync.Parser", ex);
                return;
            }

            EnsureTabExists(tabControl);
            _wpfControl.ShowCapturedText(capture, parsed.Data.Count);
        }

        private static FieldInfo FindField(Type t, string name)
        {
            for (var cursor = t; cursor != null; cursor = cursor.BaseType)
            {
                var f = cursor.GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }
    }
}
