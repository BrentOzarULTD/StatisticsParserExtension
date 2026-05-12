using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using StatisticsParser.Core.Models;

namespace StatisticsParser.Core.Formatting;

// Flattens a ParseResult into tab-separated text suitable for the clipboard.
// Output layout mirrors what StatisticsParserControl renders visually: per-statement IO grids
// (with a leading Row Num column), inline time grids, error/info/completion lines, then a
// "Totals:" section with the grand IO table and grand time table. Numbers use N0 with the
// current culture's group separator to match the WPF view.
public static class TextReportBuilder
{
    private const string CompileLabel = "SQL Server parse and compile time";
    private const string ExecutionLabel = "SQL Server Execution Times";
    private const string TotalLabel = "Total";
    private const string Tab = "\t";

    public const string SummaryNoticeText = "Summary row detected. Row not added to total.";

    private static readonly IReadOnlyDictionary<IoColumn, string> ColumnHeader
        = new Dictionary<IoColumn, string>
        {
            { IoColumn.Scan,                   "Scan Count" },
            { IoColumn.Logical,                "Logical Reads" },
            { IoColumn.Physical,               "Physical Reads" },
            { IoColumn.PageServer,             "Page Server Reads" },
            { IoColumn.ReadAhead,              "Read-Ahead Reads" },
            { IoColumn.PageServerReadAhead,    "Page Server Read-Ahead Reads" },
            { IoColumn.LobLogical,             "LOB Logical Reads" },
            { IoColumn.LobPhysical,            "LOB Physical Reads" },
            { IoColumn.LobPageServer,          "LOB Page Server Reads" },
            { IoColumn.LobReadAhead,           "LOB Read-Ahead Reads" },
            { IoColumn.LobPageServerReadAhead, "LOB Page Server Read-Ahead Reads" },
            { IoColumn.SegmentReads,           "Segment Reads" },
            { IoColumn.SegmentSkipped,         "Segment Skipped" },
            { IoColumn.PercentRead,            "% Logical Reads of Total Reads" },
        };

    public static string Build(ParseResult? parsed)
    {
        if (parsed == null || parsed.Data == null || parsed.Data.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var pendingTime = new List<TimeRow>();

        foreach (var row in parsed.Data)
        {
            if (row is TimeRow t)
            {
                if (t.Summary)
                {
                    FlushTime(sb, pendingTime);
                    AppendSummaryTime(sb, t);
                }
                else
                {
                    pendingTime.Add(t);
                }
                continue;
            }

            FlushTime(sb, pendingTime);

            switch (row)
            {
                case IoGroup g:
                    AppendIoGroup(sb, g);
                    break;
                case RowsAffectedRow ra:
                    AppendRowsAffected(sb, ra.Count);
                    break;
                case ErrorRow er:
                    AppendBlock(sb, er.Text);
                    break;
                case CompletionTimeRow ct:
                    AppendCompletion(sb, ct.Timestamp);
                    break;
                case InfoRow ir:
                    AppendBlock(sb, ir.Text);
                    break;
            }
        }

        FlushTime(sb, pendingTime);

        sb.AppendLine("Totals:");
        if (parsed.Total.IoTotal.Data.Count > 0)
        {
            AppendIoGrandTotal(sb, parsed.Total.IoTotal);
        }
        AppendGrandTime(sb, parsed.Total.CompileTotal, parsed.Total.ExecutionTotal);

        return sb.ToString();
    }

    private static void FlushTime(StringBuilder sb, List<TimeRow> pending)
    {
        if (pending.Count == 0) return;
        sb.Append(Tab).Append("CPU").Append(Tab).Append("Elapsed").AppendLine();
        foreach (var t in pending)
        {
            var label = t.RowType == RowType.CompileTime ? CompileLabel : ExecutionLabel;
            sb.Append(label).Append(Tab)
                .Append(TimeFormatter.FormatMs(t.CpuMs)).Append(Tab)
                .Append(TimeFormatter.FormatMs(t.ElapsedMs)).AppendLine();
        }
        sb.AppendLine();
        pending.Clear();
    }

    private static void AppendSummaryTime(StringBuilder sb, TimeRow t)
    {
        sb.Append(Tab).Append("CPU").Append(Tab).Append("Elapsed").AppendLine();
        var label = t.RowType == RowType.CompileTime ? CompileLabel : ExecutionLabel;
        sb.Append(label).Append(Tab)
            .Append(TimeFormatter.FormatMs(t.CpuMs)).Append(Tab)
            .Append(TimeFormatter.FormatMs(t.ElapsedMs)).AppendLine();
        sb.AppendLine(SummaryNoticeText);
        sb.AppendLine();
    }

    private static void AppendRowsAffected(StringBuilder sb, int count)
    {
        sb.Append(count.ToString("N0", CultureInfo.CurrentCulture));
        sb.Append(" row").Append(count == 1 ? string.Empty : "s").Append(" affected").AppendLine();
        sb.AppendLine();
    }

    private static void AppendBlock(StringBuilder sb, string text)
    {
        sb.AppendLine(text);
        sb.AppendLine();
    }

    private static void AppendCompletion(StringBuilder sb, DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " HH:mm:ss.fffffff zzz";
        var formatted = local.ToString(pattern, CultureInfo.CurrentCulture);
        sb.Append("Completion time: ").Append(formatted).AppendLine();
        sb.AppendLine();
    }

    // Per-statement IO grid: leading "Row Num" + "Table" columns, then user-selected metric
    // columns in source order, then PercentRead pinned to the right end (when present). The
    // final Total row leaves Row Num and PercentRead blank to mirror the WPF view.
    private static void AppendIoGroup(StringBuilder sb, IoGroup g)
    {
        var cols = SelectIoColumns(g.Columns);
        var hasPercent = g.Columns.Contains(IoColumn.PercentRead);

        sb.Append("Row Num").Append(Tab).Append("Table");
        foreach (var c in cols) sb.Append(Tab).Append(ColumnHeader[c]);
        if (hasPercent) sb.Append(Tab).Append(ColumnHeader[IoColumn.PercentRead]);
        sb.AppendLine();

        for (int i = 0; i < g.Data.Count; i++)
        {
            var r = g.Data[i];
            sb.Append((i + 1).ToString("N0", CultureInfo.CurrentCulture)).Append(Tab);
            sb.Append(r.TableName);
            foreach (var c in cols) sb.Append(Tab).Append(GetIoValue(r, c));
            if (hasPercent) sb.Append(Tab).Append(PercentFormatter.FormatPercent(r.PercentRead));
            sb.AppendLine();
        }

        sb.Append(Tab).Append(TotalLabel);
        foreach (var c in cols) sb.Append(Tab).Append(GetIoTotalValue(g.Total, c));
        if (hasPercent) sb.Append(Tab);
        sb.AppendLine();

        sb.AppendLine();
    }

    // Grand IO total (the "Totals:" section): single "Table" column at the front (no Row Num),
    // per-table aggregated rows, and a final Total row with PercentRead blank.
    private static void AppendIoGrandTotal(StringBuilder sb, IoGrandTotal g)
    {
        var cols = SelectIoColumns(g.Columns);
        var hasPercent = g.Columns.Contains(IoColumn.PercentRead);

        sb.Append("Table");
        foreach (var c in cols) sb.Append(Tab).Append(ColumnHeader[c]);
        if (hasPercent) sb.Append(Tab).Append(ColumnHeader[IoColumn.PercentRead]);
        sb.AppendLine();

        foreach (var t in g.Data)
        {
            sb.Append(t.TableName);
            foreach (var c in cols) sb.Append(Tab).Append(GetIoTotalValue(t, c));
            if (hasPercent) sb.Append(Tab).Append(PercentFormatter.FormatPercent(t.PercentRead));
            sb.AppendLine();
        }

        sb.Append(string.IsNullOrEmpty(g.Total.TableName) ? TotalLabel : g.Total.TableName);
        foreach (var c in cols) sb.Append(Tab).Append(GetIoTotalValue(g.Total, c));
        if (hasPercent) sb.Append(Tab);
        sb.AppendLine();

        sb.AppendLine();
    }

    private static void AppendGrandTime(StringBuilder sb, TimeTotal compile, TimeTotal execution)
    {
        sb.Append(Tab).Append("CPU").Append(Tab).Append("Elapsed").AppendLine();
        sb.Append(CompileLabel).Append(Tab)
            .Append(TimeFormatter.FormatMs(compile.CpuMs)).Append(Tab)
            .Append(TimeFormatter.FormatMs(compile.ElapsedMs)).AppendLine();
        sb.Append(ExecutionLabel).Append(Tab)
            .Append(TimeFormatter.FormatMs(execution.CpuMs)).Append(Tab)
            .Append(TimeFormatter.FormatMs(execution.ElapsedMs)).AppendLine();
        sb.Append(TotalLabel).Append(Tab)
            .Append(TimeFormatter.FormatMs(compile.CpuMs + execution.CpuMs)).Append(Tab)
            .Append(TimeFormatter.FormatMs(compile.ElapsedMs + execution.ElapsedMs)).AppendLine();
    }

    private static IEnumerable<IoColumn> SelectIoColumns(IList<IoColumn> all)
    {
        foreach (var c in all)
        {
            if (c == IoColumn.Table || c == IoColumn.PercentRead) continue;
            if (ColumnHeader.ContainsKey(c)) yield return c;
        }
    }

    private static string GetIoValue(IoRow r, IoColumn c) => c switch
    {
        IoColumn.Scan => r.Scan.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.Logical => r.Logical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.Physical => r.Physical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.PageServer => r.PageServer.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.ReadAhead => r.ReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.PageServerReadAhead => r.PageServerReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobLogical => r.LobLogical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobPhysical => r.LobPhysical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobPageServer => r.LobPageServer.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobReadAhead => r.LobReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobPageServerReadAhead => r.LobPageServerReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.SegmentReads => r.SegmentReads.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.SegmentSkipped => r.SegmentSkipped.ToString("N0", CultureInfo.CurrentCulture),
        _ => string.Empty,
    };

    private static string GetIoTotalValue(IoGroupTotal r, IoColumn c) => c switch
    {
        IoColumn.Scan => r.Scan.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.Logical => r.Logical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.Physical => r.Physical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.PageServer => r.PageServer.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.ReadAhead => r.ReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.PageServerReadAhead => r.PageServerReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobLogical => r.LobLogical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobPhysical => r.LobPhysical.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobPageServer => r.LobPageServer.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobReadAhead => r.LobReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.LobPageServerReadAhead => r.LobPageServerReadAhead.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.SegmentReads => r.SegmentReads.ToString("N0", CultureInfo.CurrentCulture),
        IoColumn.SegmentSkipped => r.SegmentSkipped.ToString("N0", CultureInfo.CurrentCulture),
        _ => string.Empty,
    };
}
