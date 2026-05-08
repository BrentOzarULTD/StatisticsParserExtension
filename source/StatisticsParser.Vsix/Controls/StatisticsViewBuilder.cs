using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using StatisticsParser.Core.Formatting;
using StatisticsParser.Core.Models;

namespace StatisticsParser.Vsix.Controls
{
    // Builds the WPF surface for a single ParseResult: per-statement IO + Time DataGrids,
    // grand totals, error/info/completion lines. All styling routes through theme keys defined
    // in StatisticsParserControl.xaml resources.
    internal static class StatisticsViewBuilder
    {
        private const string CompileLabel = "SQL Server parse and compile time";
        private const string ExecutionLabel = "SQL Server Execution Times";
        private const string TotalLabel = "Total";

        // Header text + IoRowDisplay property path + optional sort path (for the percent column,
        // we display a formatted string but want to sort by the underlying double).
        private static readonly IReadOnlyDictionary<IoColumn, (string Header, string Path, string SortPath)> ColumnSpec
            = new Dictionary<IoColumn, (string, string, string)>
            {
                { IoColumn.Scan,                   ("Scan Count",                          nameof(IoRowDisplay.Scan),                  null) },
                { IoColumn.Logical,                ("Logical Reads",                       nameof(IoRowDisplay.Logical),               null) },
                { IoColumn.Physical,               ("Physical Reads",                      nameof(IoRowDisplay.Physical),              null) },
                { IoColumn.PageServer,             ("Page Server Reads",                  nameof(IoRowDisplay.PageServer),            null) },
                { IoColumn.ReadAhead,              ("Read-Ahead Reads",                   nameof(IoRowDisplay.ReadAhead),             null) },
                { IoColumn.PageServerReadAhead,    ("Page Server\nRead-Ahead Reads",       nameof(IoRowDisplay.PageServerReadAhead),   null) },
                { IoColumn.LobLogical,             ("LOB \nLogical Reads",                  nameof(IoRowDisplay.LobLogical),            null) },
                { IoColumn.LobPhysical,            ("LOB \nPhysical Reads",                 nameof(IoRowDisplay.LobPhysical),           null) },
                { IoColumn.LobPageServer,          ("LOB \nPage Server Reads",              nameof(IoRowDisplay.LobPageServer),         null) },
                { IoColumn.LobReadAhead,           ("LOB \nRead-Ahead Reads",               nameof(IoRowDisplay.LobReadAhead),          null) },
                { IoColumn.LobPageServerReadAhead, ("LOB Page Server\nRead-Ahead Reads",   nameof(IoRowDisplay.LobPageServerReadAhead),null) },
                { IoColumn.SegmentReads,           ("Segment Reads",                       nameof(IoRowDisplay.SegmentReads),          null) },
                { IoColumn.SegmentSkipped,         ("Segment Skipped",                     nameof(IoRowDisplay.SegmentSkipped),        null) },
                { IoColumn.PercentRead,            ("% Logical Reads\nof Total Reads",     nameof(IoRowDisplay.PercentReadFormatted), nameof(IoRowDisplay.PercentRead)) },
            };

        public static FrameworkElement BuildEmptyState()
        {
            return new TextBox
            {
                Text = "No statistics output found in Messages tab.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
            };
        }

#if DEBUG
        // Debug-only banner: surfaces assembly version + on-disk build timestamp so the developer
        // can tell at a glance whether the running SSMS is loading the latest reinstalled VSIX.
        public static FrameworkElement BuildDebugVersionLine()
        {
            var asm = typeof(StatisticsViewBuilder).Assembly;
            var version = asm.GetName().Version;
            string built;
            try
            {
                built = System.IO.File.GetLastWriteTime(asm.Location).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            }
            catch
            {
                built = "unknown";
            }
            return new TextBox
            {
                Text = "DEBUG · v" + version + " · built " + built,
                Opacity = 0.6,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 8, 0, 0),
            };
        }
#endif

        public static FrameworkElement BuildSectionLabel(string text)
        {
            return new TextBox
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 4),
            };
        }

        public static FrameworkElement BuildRowsAffected(int count)
        {
            return new TextBox
            {
                Text = count.ToString("N0", CultureInfo.CurrentCulture) + " row" + (count == 1 ? string.Empty : "s") + " affected",
                // Bold rather than SemiBold: in Segoe UI at this size the SemiBold comma renders
                // with a stubby tail that reads as truncated next to the Bold comma in Total rows.
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 4),
            };
        }

        public static FrameworkElement BuildError(string text)
        {
            var tb = new TextBox
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 4, 0, 4),
            };
            // Bind Foreground to the VS error brush so theme switches are picked up live.
            tb.SetResourceReference(TextBox.ForegroundProperty, EnvironmentColors.ToolWindowValidationErrorTextBrushKey);
            return tb;
        }

        public static FrameworkElement BuildInfo(string text)
        {
            return new TextBox
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
            };
        }

        public static FrameworkElement BuildCompletion(DateTimeOffset timestamp)
        {
            // Locale-aware date with the time kept at tick precision (HH:mm:ss.fffffff) plus the
            // local UTC offset. Example en-US: "5/27/2025 10:32:37.8122685 -04:00".
            var local = timestamp.ToLocalTime();
            var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " HH:mm:ss.fffffff zzz";
            var formatted = local.ToString(pattern, CultureInfo.CurrentCulture);
            return new TextBox
            {
                Text = "Completion time: " + formatted,
                Margin = new Thickness(0, 4, 0, 4),
            };
        }

        public static FrameworkElement BuildIoSection(IoGroup g)
        {
            var displayRows = g.Data
                .Select((r, i) => IoRowDisplay.FromRow(r, i + 1))
                .ToList();
            var totalRow = IoRowDisplay.FromTotal(g.Total);
            // Per FUNCTIONAL.md, the Total row's RowNum cell and PercentRead cell are blank.
            totalRow.RowNumDisplay = string.Empty;
            totalRow.PercentReadFormatted = string.Empty;

            return BuildIoTable(g.Columns, displayRows, totalRow, includeRowNum: true);
        }

        public static FrameworkElement BuildGrandIoSection(IoGrandTotal grand)
        {
            var displayRows = grand.Data
                .Select(r => IoRowDisplay.FromTotal(r))
                .ToList();
            var totalRow = IoRowDisplay.FromTotal(grand.Total);
            totalRow.PercentReadFormatted = string.Empty;

            return BuildIoTable(grand.Columns, displayRows, totalRow, includeRowNum: false);
        }

        private static FrameworkElement BuildIoTable(
            IList<IoColumn> columns,
            IList<IoRowDisplay> dataRows,
            IoRowDisplay totalRow,
            bool includeRowNum)
        {
            var dataGrid = new DataGrid
            {
                CanUserSortColumns = true,
                CanUserResizeColumns = true,
                ItemsSource = dataRows,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AddIoColumns(dataGrid, columns, includeRowNum, hideRowNumHeader: false);

            var totalGrid = new DataGrid
            {
                HeadersVisibility = DataGridHeadersVisibility.None,
                CanUserSortColumns = false,
                CanUserResizeColumns = false,
                ItemsSource = new[] { totalRow },
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                FontWeight = FontWeights.Bold,
            };
            AddIoColumns(totalGrid, columns, includeRowNum, hideRowNumHeader: true);

            SyncColumnWidths(dataGrid, totalGrid);

            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(dataGrid);
            panel.Children.Add(totalGrid);
            return panel;
        }

        private static void AddIoColumns(DataGrid grid, IList<IoColumn> columns, bool includeRowNum, bool hideRowNumHeader)
        {
            if (includeRowNum)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = hideRowNumHeader ? string.Empty : "Row Num",
                    Binding = new Binding(nameof(IoRowDisplay.RowNumDisplay)),
                    SortMemberPath = nameof(IoRowDisplay.RowNum),
                    ElementStyle = RightAlignedTextStyle,
                    HeaderStyle = RightAlignedHeaderStyle,
                });
            }

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Table",
                Binding = new Binding(nameof(IoRowDisplay.TableName)),
                SortMemberPath = nameof(IoRowDisplay.TableName),
                ElementStyle = TableNameElementStyle,
            });

            // Render columns in the order the parser supplied (excluding Table — we always add it
            // ourselves above — and PercentRead, which sits at the right end).
            foreach (var col in columns)
            {
                if (col == IoColumn.Table || col == IoColumn.PercentRead) continue;
                if (!ColumnSpec.TryGetValue(col, out var spec)) continue;
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = spec.Header,
                    Binding = new Binding(spec.Path) { Converter = IntegerThousandsConverter.Instance },
                    ElementStyle = RightAlignedTextStyle,
                    HeaderStyle = RightAlignedHeaderStyle,
                });
            }
            if (columns.Contains(IoColumn.PercentRead))
            {
                var spec = ColumnSpec[IoColumn.PercentRead];
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = spec.Header,
                    Binding = new Binding(spec.Path),
                    SortMemberPath = spec.SortPath,
                    ElementStyle = RightAlignedTextStyle,
                    HeaderStyle = RightAlignedHeaderStyle,
                });
            }
        }

        private static readonly Style RightAlignedTextStyle = CreateRightAlignedTextStyle();
        private static readonly Style RightAlignedHeaderStyle = CreateRightAlignedHeaderStyle();
        private static readonly Style TableNameElementStyle = CreateTableNameElementStyle();

        private static Style CreateRightAlignedTextStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            return style;
        }

        // Per-cell tooltip for the Table column. Bound to IoRowDisplay.TableNameFull, which is
        // null for any row whose displayed name was not truncated (non-temp tables, or temp tables
        // with no 7+ underscore run). A null ToolTip suppresses the popup in WPF, so non-temp
        // cells get no hover behaviour.
        private static Style CreateTableNameElementStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(
                TextBlock.ToolTipProperty,
                new Binding(nameof(IoRowDisplay.TableNameFull))));
            return style;
        }

        // HorizontalContentAlignment=Stretch lets the templated TextBlock fill the header cell
        // width so per-line TextAlignment=Right anchors each line to the right edge — needed
        // for multi-line headers like "Page Server\nRead-Ahead Reads".
        //
        // Setting an explicit HeaderStyle on a DataGridTextColumn replaces the implicit
        // DataGridColumnHeader style from StatisticsParserControl.xaml entirely (WPF does not
        // merge implicit + explicit styles), so the themed Background/Foreground/border setters
        // must be duplicated here as DynamicResource references — otherwise these headers do not
        // repaint on theme switch and stay default-white in dark themes.
        private static Style CreateRightAlignedHeaderStyle()
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.BackgroundProperty,
                new DynamicResourceExtension(HeaderColors.DefaultBrushKey)));
            style.Setters.Add(new Setter(Control.ForegroundProperty,
                new DynamicResourceExtension(HeaderColors.DefaultTextBrushKey)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty,
                new DynamicResourceExtension(EnvironmentColors.PanelBorderBrushKey)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            style.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            var template = new DataTemplate();
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding());
            tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            template.VisualTree = tb;
            style.Setters.Add(new Setter(DataGridColumnHeader.ContentTemplateProperty, template));
            return style;
        }

        // Reconciles total-row column widths to the data-grid's after both have measured. Runs
        // until at least one frame produced positive ActualWidths, then unhooks. LayoutUpdated
        // fires per frame; the unhook keeps it from running indefinitely.
        private static void SyncColumnWidths(DataGrid source, DataGrid target)
        {
            EventHandler handler = null;
            handler = (s, e) =>
            {
                bool any = false;
                int n = Math.Min(source.Columns.Count, target.Columns.Count);
                for (int i = 0; i < n; i++)
                {
                    double w = Math.Max(source.Columns[i].ActualWidth, target.Columns[i].ActualWidth);
                    if (w > 0)
                    {
                        var len = new DataGridLength(w);
                        source.Columns[i].Width = len;
                        target.Columns[i].Width = len;
                        any = true;
                    }
                }
                if (any)
                {
                    source.LayoutUpdated -= handler;
                }
            };
            source.LayoutUpdated += handler;
        }

        public static FrameworkElement BuildTimeSection(IList<TimeRow> rows)
        {
            var displayRows = rows
                .Select(r => new TimeRowDisplay
                {
                    Label = r.RowType == RowType.CompileTime ? CompileLabel : ExecutionLabel,
                    Cpu = TimeFormatter.FormatMs(r.CpuMs),
                    Elapsed = TimeFormatter.FormatMs(r.ElapsedMs),
                })
                .ToList();
            return BuildTimeGrid(displayRows);
        }

        public static FrameworkElement BuildGrandTimeSection(TimeTotal compile, TimeTotal execution)
        {
            var dataRows = new List<TimeRowDisplay>
            {
                new TimeRowDisplay
                {
                    Label = CompileLabel,
                    Cpu = TimeFormatter.FormatMs(compile.CpuMs),
                    Elapsed = TimeFormatter.FormatMs(compile.ElapsedMs),
                },
                new TimeRowDisplay
                {
                    Label = ExecutionLabel,
                    Cpu = TimeFormatter.FormatMs(execution.CpuMs),
                    Elapsed = TimeFormatter.FormatMs(execution.ElapsedMs),
                },
            };
            var totalRow = new TimeRowDisplay
            {
                Label = TotalLabel,
                Cpu = TimeFormatter.FormatMs(compile.CpuMs + execution.CpuMs),
                Elapsed = TimeFormatter.FormatMs(compile.ElapsedMs + execution.ElapsedMs),
            };

            var dataGrid = new DataGrid
            {
                CanUserSortColumns = false,
                CanUserResizeColumns = true,
                ItemsSource = dataRows,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AddTimeColumns(dataGrid);

            var totalGrid = new DataGrid
            {
                HeadersVisibility = DataGridHeadersVisibility.None,
                CanUserSortColumns = false,
                CanUserResizeColumns = false,
                ItemsSource = new[] { totalRow },
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                FontWeight = FontWeights.Bold,
            };
            AddTimeColumns(totalGrid);

            SyncColumnWidths(dataGrid, totalGrid);

            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(dataGrid);
            panel.Children.Add(totalGrid);
            return panel;
        }

        private static FrameworkElement BuildTimeGrid(IList<TimeRowDisplay> rows)
        {
            var grid = new DataGrid
            {
                CanUserSortColumns = false,
                CanUserResizeColumns = true,
                ItemsSource = rows,
                Margin = new Thickness(0, 4, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AddTimeColumns(grid);
            return grid;
        }

        private static void AddTimeColumns(DataGrid grid)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = string.Empty,
                Binding = new Binding(nameof(TimeRowDisplay.Label)),
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "CPU",
                Binding = new Binding(nameof(TimeRowDisplay.Cpu)),
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Elapsed",
                Binding = new Binding(nameof(TimeRowDisplay.Elapsed)),
            });
        }

        // Public so XAML data binding can resolve property names. Mutable POCOs by intent — they
        // are rebuilt on every Render() call.
        public sealed class IoRowDisplay
        {
            public int? RowNum { get; set; }
            public string RowNumDisplay { get; set; }
            public string TableName { get; set; }
            // Full original name used as the Table-cell tooltip when TableName was truncated by
            // TableNameFormatter; null otherwise (null suppresses the WPF tooltip).
            public string TableNameFull { get; set; }
            public int Scan { get; set; }
            public int Logical { get; set; }
            public int Physical { get; set; }
            public int PageServer { get; set; }
            public int ReadAhead { get; set; }
            public int PageServerReadAhead { get; set; }
            public int LobLogical { get; set; }
            public int LobPhysical { get; set; }
            public int LobPageServer { get; set; }
            public int LobReadAhead { get; set; }
            public int LobPageServerReadAhead { get; set; }
            public int SegmentReads { get; set; }
            public int SegmentSkipped { get; set; }
            public double PercentRead { get; set; }
            public string PercentReadFormatted { get; set; }

            public static IoRowDisplay FromRow(IoRow r, int rowNum) => new IoRowDisplay
            {
                RowNum = rowNum,
                RowNumDisplay = rowNum.ToString("N0", CultureInfo.CurrentCulture),
                TableName = TableNameFormatter.FormatForDisplay(r.TableName),
                TableNameFull = TableNameFormatter.IsTruncated(r.TableName) ? r.TableName : null,
                Scan = r.Scan,
                Logical = r.Logical,
                Physical = r.Physical,
                PageServer = r.PageServer,
                ReadAhead = r.ReadAhead,
                PageServerReadAhead = r.PageServerReadAhead,
                LobLogical = r.LobLogical,
                LobPhysical = r.LobPhysical,
                LobPageServer = r.LobPageServer,
                LobReadAhead = r.LobReadAhead,
                LobPageServerReadAhead = r.LobPageServerReadAhead,
                SegmentReads = r.SegmentReads,
                SegmentSkipped = r.SegmentSkipped,
                PercentRead = r.PercentRead,
                PercentReadFormatted = PercentFormatter.FormatPercent(r.PercentRead),
            };

            public static IoRowDisplay FromTotal(IoGroupTotal t)
            {
                var rawName = string.IsNullOrEmpty(t.TableName) ? TotalLabel : t.TableName;
                return new IoRowDisplay
                {
                    RowNum = null,
                    RowNumDisplay = string.Empty,
                    TableName = TableNameFormatter.FormatForDisplay(rawName),
                    TableNameFull = TableNameFormatter.IsTruncated(rawName) ? rawName : null,
                    Scan = t.Scan,
                    Logical = t.Logical,
                    Physical = t.Physical,
                    PageServer = t.PageServer,
                    ReadAhead = t.ReadAhead,
                    PageServerReadAhead = t.PageServerReadAhead,
                    LobLogical = t.LobLogical,
                    LobPhysical = t.LobPhysical,
                    LobPageServer = t.LobPageServer,
                    LobReadAhead = t.LobReadAhead,
                    LobPageServerReadAhead = t.LobPageServerReadAhead,
                    SegmentReads = t.SegmentReads,
                    SegmentSkipped = t.SegmentSkipped,
                    PercentRead = t.PercentRead,
                    PercentReadFormatted = PercentFormatter.FormatPercent(t.PercentRead),
                };
            }
        }

        public sealed class TimeRowDisplay
        {
            public string Label { get; set; }
            public string Cpu { get; set; }
            public string Elapsed { get; set; }
        }

        // WPF Binding.StringFormat resolves against ConverterCulture, which falls back to en-US
        // when unset — not CultureInfo.CurrentCulture. This converter formats with the user's
        // current locale's group separator.
        private sealed class IntegerThousandsConverter : IValueConverter
        {
            public static readonly IntegerThousandsConverter Instance = new IntegerThousandsConverter();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                => value is int i ? i.ToString("N0", CultureInfo.CurrentCulture) : value?.ToString();

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }
}
