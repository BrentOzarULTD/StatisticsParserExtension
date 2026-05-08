using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using StatisticsParser.Core.Formatting;
using StatisticsParser.Core.Models;
using StatisticsParser.Vsix.Options;

namespace StatisticsParser.Vsix.Controls
{
    public partial class StatisticsParserControl : UserControl
    {
        // Routed command so Ctrl+Shift+C and the panel's "Copy all output" context-menu item
        // both invoke the same handler. Bound from XAML via {x:Static local:...}.
        public static readonly RoutedUICommand CopyAllOutputCommand = new RoutedUICommand(
            text: "Copy all output",
            name: nameof(CopyAllOutputCommand),
            ownerType: typeof(StatisticsParserControl));

        private const string ConvertCompletionTimeMoniker = "statisticsParser.convertCompletionTimeToLocalTime";
        private const string TempTableNamesMoniker = "statisticsParser.tempTableNamesMode";

        private ParseResult _lastParsed;
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
                _settingsSubscription = reader.SubscribeToChanges(OnSettingsChanged,
                    ConvertCompletionTimeMoniker,
                    TempTableNamesMoniker);
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
            // Migration block in registration.json mirrors the unified-settings value into the
            // same SettingsManager path that BaseOptionModel<StatisticsParserOptions> reads, so
            // a Load() refresh is enough — no need to read the unified-settings store directly.
            StatisticsParserOptions.Instance.Load();
            if (_lastParsed == null) return;
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (_lastParsed != null) Render(_lastParsed);
            }).FileAndForget("StatisticsParser/OnSettingsChanged");
#pragma warning restore VSSDK007
        }

        public void Render(ParseResult parsed)
        {
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
    }
}
