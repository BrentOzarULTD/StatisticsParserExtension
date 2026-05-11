using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using StatisticsParser.Core.Formatting;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using StatisticsParser.Vsix.Options;

namespace StatisticsParser.Vsix.Controls
{
    public partial class StatisticsParserControl : UserControl
    {
        // Routed command so Ctrl+Shift+C and the panel's "Copy All" context-menu item
        // both invoke the same handler. Bound from XAML via {x:Static local:...}.
        public static readonly RoutedUICommand CopyAllOutputCommand = new RoutedUICommand(
            text: "Copy All",
            name: nameof(CopyAllOutputCommand),
            ownerType: typeof(StatisticsParserControl));

        public static readonly RoutedUICommand ShowAboutCommand = new RoutedUICommand(
            text: "About Statistics Parser",
            name: nameof(ShowAboutCommand),
            ownerType: typeof(StatisticsParserControl));

        private ParseResult _lastParsed;
        // Captured Messages text from the most recent Render call. Held so that an option change
        // affecting parser output (currently only SuppressZeroColumns) can re-parse and refresh
        // without requiring the user to rerun the query.
        private string _lastText;
        private IDisposable _settingsSubscription;

        public StatisticsParserControl()
        {
            InitializeComponent();
            // Loaded/Unloaded keep the unified-settings change subscription from rooting
            // a control whose Parse Statistics tab has been removed by SSMS.
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var manager = await VS.GetRequiredServiceAsync<SVsUnifiedSettingsManager, ISettingsManager>();
                var reader = manager.GetWriter("StatisticsParser");
                StatisticsParserOptions.Refresh(reader);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ApplyFontSettings();
                _settingsSubscription = reader.SubscribeToChanges(OnSettingsChanged,
                    StatisticsParserOptions.ConvertCompletionTimeToLocalTimeMoniker,
                    StatisticsParserOptions.TempTableNamesMoniker,
                    StatisticsParserOptions.FontSizeMoniker,
                    StatisticsParserOptions.SuppressZeroColumnsMoniker);
            }).FileAndForget("StatisticsParser/SubscribeUnified");
#pragma warning restore VSSDK007
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _settingsSubscription?.Dispose();
            _settingsSubscription = null;
        }

        private void OnSettingsChanged(SettingsUpdate update)
        {
            // Only SuppressZeroColumns affects parser output — the other three options just
            // change rendering. Avoid re-parsing for those, otherwise every Font Size / Temp
            // Table Names / Completion Time toggle drags the (potentially large) Messages
            // text through Parser.ParseData on the UI thread.
            bool needsReparse = update?.ChangedSettingMonikers != null
                && update.ChangedSettingMonikers.Contains(StatisticsParserOptions.SuppressZeroColumnsMoniker);

#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var manager = await VS.GetRequiredServiceAsync<SVsUnifiedSettingsManager, ISettingsManager>();
                var reader = manager.GetWriter("StatisticsParser");
                StatisticsParserOptions.Refresh(reader);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ApplyFontSettings();
                if (needsReparse && _lastText != null)
                    Render(_lastText, Parser.ParseData(_lastText, ParserLanguage.English, StatisticsParserOptions.SuppressZeroColumns));
                else if (_lastParsed != null)
                    Render(_lastText, _lastParsed);
            }).FileAndForget("StatisticsParser/OnSettingsChanged");
#pragma warning restore VSSDK007
        }

        private void ApplyFontSettings()
        {
            // Pull the user's VS Environment Font baseline so Small/Normal/Large/Extra Large are
            // relative to whatever the user already configured under Tools > Options > Environment
            // > Fonts and Colors. Falls back to WPF's default 12 DIP when the resource cannot be
            // resolved (e.g. when hosted outside the VS shell during tests).
            double baseSize = 12.0;
            var sizeResource = TryFindResource(VsFonts.EnvironmentFontSizeKey);
            if (sizeResource is double d) baseSize = d;

            FontSize = baseSize * StatisticsParserOptions.ScaleFactor(StatisticsParserOptions.FontSize);

            if (TryFindResource(VsFonts.EnvironmentFontFamilyKey) is FontFamily family)
            {
                FontFamily = family;
            }
        }

        // text is the raw Messages-tab string the parsed result came from. May be null when the
        // caller has no text to hand back (e.g. an internal re-render with already-parsed data);
        // when null, OnSettingsChanged falls back to re-rendering _lastParsed without re-parsing.
        public void Render(string text, ParseResult parsed)
        {
            _lastText = text;
            _lastParsed = parsed;
            CommandManager.InvalidateRequerySuggested();
            ContentPanel.Children.Clear();

            if (parsed == null || parsed.Data == null || parsed.Data.Count == 0)
            {
                ContentPanel.Children.Add(StatisticsViewBuilder.BuildEmptyState());
            }
            else
            {
                var pendingTime = new List<TimeRow>();

                foreach (var row in parsed.Data)
                {
                    if (row is TimeRow t)
                    {
                        if (!t.Summary) pendingTime.Add(t);
                        continue;
                    }

                    FlushTimeGroup(pendingTime);

                    switch (row)
                    {
                        case IoGroup g:
                            ContentPanel.Children.Add(StatisticsViewBuilder.BuildIoSection(g));
                            break;
                        case RowsAffectedRow ra:
                            ContentPanel.Children.Add(StatisticsViewBuilder.BuildRowsAffected(ra.Count));
                            break;
                        case ErrorRow er:
                            ContentPanel.Children.Add(StatisticsViewBuilder.BuildError(er.Text));
                            break;
                        case CompletionTimeRow ct:
                            ContentPanel.Children.Add(StatisticsViewBuilder.BuildCompletion(ct.Timestamp));
                            break;
                        case InfoRow ir:
                            ContentPanel.Children.Add(StatisticsViewBuilder.BuildInfo(ir.Text));
                            break;
                    }
                }

                FlushTimeGroup(pendingTime);

                ContentPanel.Children.Add(StatisticsViewBuilder.BuildSectionLabel("Totals:"));
                ContentPanel.Children.Add(StatisticsViewBuilder.BuildGrandIoSection(parsed.Total.IoTotal));
                ContentPanel.Children.Add(StatisticsViewBuilder.BuildGrandTimeSection(
                    parsed.Total.CompileTotal, parsed.Total.ExecutionTotal));
            }

#if DEBUG
            ContentPanel.Children.Add(StatisticsViewBuilder.BuildDebugVersionLine());
#endif
        }

        private void FlushTimeGroup(List<TimeRow> pending)
        {
            if (pending.Count == 0) return;
            ContentPanel.Children.Add(StatisticsViewBuilder.BuildTimeSection(pending));
            pending.Clear();
        }

        private void OnCopyAllCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _lastParsed?.Data != null && _lastParsed.Data.Count > 0;
            e.Handled = true;
        }

        private void OnCopyAllExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var text = TextReportBuilder.Build(_lastParsed);
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception)
            {
                // Clipboard.SetText can throw if another process holds the clipboard. Silently
                // swallow — the user can retry, and we don't have a host for an error toast here.
            }
            e.Handled = true;
        }

        private void OnShowAboutExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dlg = new AboutDialog();
            IntPtr owner = IntPtr.Zero;
            if (Package.GetGlobalService(typeof(SVsUIShell)) is IVsUIShell uiShell
                && ErrorHandler.Succeeded(uiShell.GetDialogOwnerHwnd(out owner))
                && owner != IntPtr.Zero)
            {
                new WindowInteropHelper(dlg).Owner = owner;
            }
            dlg.ShowDialog();
            e.Handled = true;
        }
    }
}
