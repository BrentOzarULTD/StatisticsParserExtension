using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using StatisticsParser.Core.Models;

[assembly: InternalsVisibleTo("StatisticsParser.Core.Tests")]

namespace StatisticsParser.Core.Parsing;

public static class Parser
{
    private static readonly Regex TimeNumberRegex =
        new Regex(@"(-?\d+)\s*ms", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IoSegmentRegex =
        new Regex(@"^(.+?)\s+(-?\d+)$", RegexOptions.Compiled);

    private static readonly Regex RowsAffectedNumberRegex =
        new Regex(@"\((\d+)\s", RegexOptions.Compiled);

    private static readonly char[] LineSeparators = { '\n' };
    private static readonly char[] SegmentTrimChars = { '.', ' ', '\t' };

    public static ParseResult ParseData(string text)
        => ParseData(text, ParserLanguage.English);

    public static ParseResult ParseData(string text, ParserLanguage lang)
    {
        var result = new ParseResult();
        if (string.IsNullOrEmpty(text))
            return result;

        var ctx = new ParseContext
        {
            Lang = lang,
            Lines = text.Split(LineSeparators, StringSplitOptions.None),
            Result = result,
            ExecutionTotal = result.Total.ExecutionTotal,
            CompileTotal = result.Total.CompileTotal,
            GrandTotalData = result.Total.IoTotal.Data,
            GrandTotalColumns = result.Total.IoTotal.Columns,
            TableNameRegex = new Regex(
                $@"^\s*{Regex.Escape(lang.Table)}\s+['""]([^'""]+)['""]\s*\.\s*",
                RegexOptions.IgnoreCase)
        };

        for (ctx.LineIndex = 0; ctx.LineIndex < ctx.Lines.Length; ctx.LineIndex++)
        {
            var line = ctx.Lines[ctx.LineIndex];
            if (line.Length > 0 && line[line.Length - 1] == '\r')
                line = line.Substring(0, line.Length - 1);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var rowType = DetermineRowType(line, lang);

            if (ctx.PrevRowType == RowType.IO && rowType != RowType.IO && ctx.CurrentGroup != null)
            {
                FinalizeIoGroup(ctx.CurrentGroup, ctx.GrandTotalColumns);
                ctx.CurrentGroup = null;
            }

            switch (rowType)
            {
                case RowType.IO:
                    HandleIo(ctx, line, ref rowType);
                    break;
                case RowType.ExecutionTime:
                case RowType.CompileTime:
                    HandleTimeHeader(ctx, line, rowType);
                    break;
                case RowType.RowsAffected:
                    HandleRowsAffected(ctx, line);
                    break;
                case RowType.Error:
                    HandleError(ctx, line);
                    break;
                case RowType.CompletionTime:
                    HandleCompletionTime(ctx, line);
                    break;
                default:
                    result.Data.Add(new InfoRow { Text = line });
                    break;
            }

            ctx.PrevRowType = rowType;
        }

        if (ctx.CurrentGroup != null)
        {
            FinalizeIoGroup(ctx.CurrentGroup, ctx.GrandTotalColumns);
            ctx.CurrentGroup = null;
        }

        FinalizeGrandTotal(result.Total.IoTotal);
        result.TableCount = ctx.TableCount;
        return result;
    }

    internal static RowType DetermineRowType(string line, ParserLanguage lang)
    {
        if (line == null) return RowType.None;

        var trimmedStart = line.TrimStart();
        if (trimmedStart.Length > lang.Table.Length
            && trimmedStart.StartsWith(lang.Table, StringComparison.OrdinalIgnoreCase))
        {
            var ch = trimmedStart[lang.Table.Length];
            if (ch == ' ' || ch == '\'' || ch == '"' || ch == '\t')
                return RowType.IO;
        }

        var trimmed = line.Trim();
        if (string.Equals(trimmed, lang.ExecutionTime, StringComparison.OrdinalIgnoreCase))
            return RowType.ExecutionTime;
        if (string.Equals(trimmed, lang.CompileTime, StringComparison.OrdinalIgnoreCase))
            return RowType.CompileTime;

        for (int i = 0; i < lang.RowsAffected.Count; i++)
        {
            if (line.IndexOf(lang.RowsAffected[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return RowType.RowsAffected;
        }

        if (lang.ErrorMsg.Length > 0
            && line.StartsWith(lang.ErrorMsg, StringComparison.Ordinal))
        {
            if (line.Length == lang.ErrorMsg.Length || line[lang.ErrorMsg.Length] == ' ')
                return RowType.Error;
        }

        if (lang.CompletionTimeLabel.Length > 0
            && line.StartsWith(lang.CompletionTimeLabel, StringComparison.Ordinal))
            return RowType.CompletionTime;

        return RowType.None;
    }

    internal static IoRow? ParseIoLine(
        string line,
        ParserLanguage lang,
        Regex tableNameRegex,
        out List<IoColumn> rowColumns)
    {
        rowColumns = new List<IoColumn>();
        var match = tableNameRegex.Match(line);
        if (!match.Success) return null;

        var row = new IoRow { TableName = match.Groups[1].Value };
        rowColumns.Add(IoColumn.Table);

        var rest = line.Substring(match.Index + match.Length);
        var segments = rest.Split(new[] { ", " }, StringSplitOptions.None);
        foreach (var raw in segments)
        {
            var seg = raw.Trim().TrimEnd(SegmentTrimChars).TrimEnd();
            if (seg.Length == 0) continue;

            var m = IoSegmentRegex.Match(seg);
            if (!m.Success) continue;

            if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                continue;

            var col = lang.DetermineIoColumn(m.Groups[1].Value);
            if (col == IoColumn.NotFound) continue;

            AssignColumn(row, col, value);
            if (!rowColumns.Contains(col))
                rowColumns.Add(col);
        }

        return row;
    }

    internal static (int cpuMs, int elapsedMs, bool ok) ParseTimeLine(string line, ParserLanguage lang)
    {
        int cpu = 0;
        int elapsed = 0;
        bool foundCpu = false;
        bool foundElapsed = false;

        if (lang.CpuTime.Length > 0)
        {
            var idx = line.IndexOf(lang.CpuTime, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var m = TimeNumberRegex.Match(line, idx + lang.CpuTime.Length);
                if (m.Success
                    && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c))
                {
                    cpu = c;
                    foundCpu = true;
                }
            }
        }

        if (lang.ElapsedTime.Length > 0)
        {
            var idx = line.IndexOf(lang.ElapsedTime, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var m = TimeNumberRegex.Match(line, idx + lang.ElapsedTime.Length);
                if (m.Success
                    && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var e))
                {
                    elapsed = e;
                    foundElapsed = true;
                }
            }
        }

        return (cpu, elapsed, foundCpu && foundElapsed);
    }

    internal static bool DetermineSummaryRow(TimeRow row, TimeTotal executionTotal, TimeTotal compileTotal)
    {
        var expectedCpu = executionTotal.CpuMs + compileTotal.CpuMs;
        var expectedElapsed = executionTotal.ElapsedMs + compileTotal.ElapsedMs;
        return row.CpuMs == expectedCpu
            && Math.Abs(row.ElapsedMs - expectedElapsed) <= 5;
    }

    internal static void ProcessGrandTotal(IoRow row, List<IoGroupTotal> grandTotal)
    {
        IoGroupTotal? entry = null;
        for (int i = 0; i < grandTotal.Count; i++)
        {
            if (string.Equals(grandTotal[i].TableName, row.TableName, StringComparison.Ordinal))
            {
                entry = grandTotal[i];
                break;
            }
        }
        if (entry == null)
        {
            entry = new IoGroupTotal { TableName = row.TableName };
            grandTotal.Add(entry);
        }
        entry.Scan += row.Scan;
        entry.Logical += row.Logical;
        entry.Physical += row.Physical;
        entry.PageServer += row.PageServer;
        entry.ReadAhead += row.ReadAhead;
        entry.PageServerReadAhead += row.PageServerReadAhead;
        entry.LobLogical += row.LobLogical;
        entry.LobPhysical += row.LobPhysical;
        entry.LobPageServer += row.LobPageServer;
        entry.LobReadAhead += row.LobReadAhead;
        entry.LobPageServerReadAhead += row.LobPageServerReadAhead;
        entry.SegmentReads += row.SegmentReads;
        entry.SegmentSkipped += row.SegmentSkipped;
    }

    internal static void FinalizeIoGroup(IoGroup group, List<IoColumn> grandTotalColumns)
    {
        if (group.Data.Count == 0) return;

        var total = new IoGroupTotal();
        foreach (var r in group.Data)
        {
            total.Scan += r.Scan;
            total.Logical += r.Logical;
            total.Physical += r.Physical;
            total.PageServer += r.PageServer;
            total.ReadAhead += r.ReadAhead;
            total.PageServerReadAhead += r.PageServerReadAhead;
            total.LobLogical += r.LobLogical;
            total.LobPhysical += r.LobPhysical;
            total.LobPageServer += r.LobPageServer;
            total.LobReadAhead += r.LobReadAhead;
            total.LobPageServerReadAhead += r.LobPageServerReadAhead;
            total.SegmentReads += r.SegmentReads;
            total.SegmentSkipped += r.SegmentSkipped;
        }

        foreach (var r in group.Data)
        {
            r.PercentRead = total.Logical > 0
                ? ((double)r.Logical / total.Logical) * 100.0
                : 0.0;
        }

        if (group.Columns.Contains(IoColumn.Logical) && !group.Columns.Contains(IoColumn.PercentRead))
            group.Columns.Add(IoColumn.PercentRead);

        group.Total = total;

        SuppressZeroColumns(group);

        foreach (var c in group.Columns)
        {
            if (!grandTotalColumns.Contains(c))
                grandTotalColumns.Add(c);
        }
    }

    internal static void FinalizeGrandTotal(IoGrandTotal gt)
    {
        if (gt.Data.Count == 0) return;

        gt.Data.Sort((a, b) => string.Compare(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase));

        var total = new IoGroupTotal();
        foreach (var e in gt.Data)
        {
            total.Scan += e.Scan;
            total.Logical += e.Logical;
            total.Physical += e.Physical;
            total.PageServer += e.PageServer;
            total.ReadAhead += e.ReadAhead;
            total.PageServerReadAhead += e.PageServerReadAhead;
            total.LobLogical += e.LobLogical;
            total.LobPhysical += e.LobPhysical;
            total.LobPageServer += e.LobPageServer;
            total.LobReadAhead += e.LobReadAhead;
            total.LobPageServerReadAhead += e.LobPageServerReadAhead;
            total.SegmentReads += e.SegmentReads;
            total.SegmentSkipped += e.SegmentSkipped;
        }

        foreach (var e in gt.Data)
        {
            e.PercentRead = total.Logical > 0
                ? ((double)e.Logical / total.Logical) * 100.0
                : 0.0;
        }

        gt.Total = total;

        var keep = new List<IoColumn>(gt.Columns.Count);
        var hasPercentRead = false;
        foreach (var col in gt.Columns)
        {
            if (col == IoColumn.PercentRead)
            {
                hasPercentRead = true;
                continue;
            }
            if (col == IoColumn.Table)
            {
                keep.Add(col);
                continue;
            }
            bool anyNonZero = false;
            foreach (var e in gt.Data)
            {
                if (GetColumnValue(e, col) != 0) { anyNonZero = true; break; }
            }
            if (anyNonZero) keep.Add(col);
        }
        if (hasPercentRead) keep.Add(IoColumn.PercentRead);
        gt.Columns = keep;
    }

    private static void HandleIo(ParseContext ctx, string line, ref RowType rowType)
    {
        var row = ParseIoLine(line, ctx.Lang, ctx.TableNameRegex, out var rowCols);
        if (row == null)
        {
            ctx.Result.Data.Add(new InfoRow { Text = line });
            rowType = RowType.None;
            return;
        }

        if (ctx.CurrentGroup == null)
        {
            ctx.CurrentGroup = new IoGroup { TableId = $"resultTable_{ctx.TableCount}" };
            ctx.Result.Data.Add(ctx.CurrentGroup);
            ctx.TableCount++;
        }

        var isSegmentContinuation = (row.SegmentReads > 0 || row.SegmentSkipped > 0)
            && ctx.CurrentGroup.Data.Count > 0
            && IsSegmentOnlyRow(row);

        if (isSegmentContinuation)
        {
            var last = ctx.CurrentGroup.Data[ctx.CurrentGroup.Data.Count - 1];
            last.SegmentReads += row.SegmentReads;
            last.SegmentSkipped += row.SegmentSkipped;

            if (rowCols.Contains(IoColumn.SegmentReads) && !ctx.CurrentGroup.Columns.Contains(IoColumn.SegmentReads))
                ctx.CurrentGroup.Columns.Add(IoColumn.SegmentReads);
            if (rowCols.Contains(IoColumn.SegmentSkipped) && !ctx.CurrentGroup.Columns.Contains(IoColumn.SegmentSkipped))
                ctx.CurrentGroup.Columns.Add(IoColumn.SegmentSkipped);

            for (int i = 0; i < ctx.GrandTotalData.Count; i++)
            {
                if (string.Equals(ctx.GrandTotalData[i].TableName, last.TableName, StringComparison.Ordinal))
                {
                    ctx.GrandTotalData[i].SegmentReads += row.SegmentReads;
                    ctx.GrandTotalData[i].SegmentSkipped += row.SegmentSkipped;
                    break;
                }
            }
        }
        else
        {
            ctx.CurrentGroup.Data.Add(row);
            foreach (var c in rowCols)
            {
                if (!ctx.CurrentGroup.Columns.Contains(c))
                    ctx.CurrentGroup.Columns.Add(c);
            }
            ProcessGrandTotal(row, ctx.GrandTotalData);
        }
    }

    private static void HandleTimeHeader(ParseContext ctx, string headerLine, RowType headerType)
    {
        if (ctx.LineIndex + 1 >= ctx.Lines.Length)
        {
            ctx.Result.Data.Add(new InfoRow { Text = headerLine });
            return;
        }

        ctx.LineIndex++;
        var dataLine = ctx.Lines[ctx.LineIndex];
        if (dataLine.Length > 0 && dataLine[dataLine.Length - 1] == '\r')
            dataLine = dataLine.Substring(0, dataLine.Length - 1);

        var (cpuMs, elapsedMs, ok) = ParseTimeLine(dataLine, ctx.Lang);
        if (!ok)
        {
            ctx.Result.Data.Add(new InfoRow { Text = headerLine });
            ctx.Result.Data.Add(new InfoRow { Text = dataLine });
            return;
        }

        var row = new TimeRow
        {
            RowType = headerType,
            CpuMs = cpuMs,
            ElapsedMs = elapsedMs
        };

        if (headerType == RowType.ExecutionTime)
        {
            row.Summary = DetermineSummaryRow(row, ctx.ExecutionTotal, ctx.CompileTotal);
            if (!row.Summary)
            {
                ctx.ExecutionTotal.CpuMs += cpuMs;
                ctx.ExecutionTotal.ElapsedMs += elapsedMs;
            }
        }
        else
        {
            ctx.CompileTotal.CpuMs += cpuMs;
            ctx.CompileTotal.ElapsedMs += elapsedMs;
        }

        ctx.Result.Data.Add(row);
    }

    private static void HandleRowsAffected(ParseContext ctx, string line)
    {
        var count = 0;
        var m = RowsAffectedNumberRegex.Match(line);
        if (m.Success)
            int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
        ctx.Result.Data.Add(new RowsAffectedRow { Count = count });
    }

    private static void HandleError(ParseContext ctx, string line)
    {
        ctx.Result.Data.Add(new ErrorRow { Text = line });
        if (ctx.LineIndex + 1 < ctx.Lines.Length)
        {
            ctx.LineIndex++;
            var detail = ctx.Lines[ctx.LineIndex];
            if (detail.Length > 0 && detail[detail.Length - 1] == '\r')
                detail = detail.Substring(0, detail.Length - 1);
            ctx.Result.Data.Add(new ErrorRow { Text = detail });
        }
    }

    private static void HandleCompletionTime(ParseContext ctx, string line)
    {
        var payload = line.Substring(ctx.Lang.CompletionTimeLabel.Length).Trim();
        // Preserve the server's reported offset on the parsed DateTimeOffset so the rendering
        // layer can display either the original offset (when "Convert Time" is unchecked) or
        // the local-time conversion (when checked) from the same Timestamp value.
        if (DateTimeOffset.TryParse(
                payload,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var ts))
        {
            ctx.Result.Data.Add(new CompletionTimeRow { Timestamp = ts });
        }
        else
        {
            ctx.Result.Data.Add(new InfoRow { Text = line });
        }
    }

    private static bool IsSegmentOnlyRow(IoRow row)
    {
        return row.Scan == 0
            && row.Logical == 0
            && row.Physical == 0
            && row.PageServer == 0
            && row.ReadAhead == 0
            && row.PageServerReadAhead == 0
            && row.LobLogical == 0
            && row.LobPhysical == 0
            && row.LobPageServer == 0
            && row.LobReadAhead == 0
            && row.LobPageServerReadAhead == 0;
    }

    private static void SuppressZeroColumns(IoGroup group)
    {
        var keep = new List<IoColumn>(group.Columns.Count);
        foreach (var col in group.Columns)
        {
            if (col == IoColumn.Table || col == IoColumn.PercentRead)
            {
                keep.Add(col);
                continue;
            }
            bool anyNonZero = false;
            foreach (var r in group.Data)
            {
                if (GetColumnValue(r, col) != 0) { anyNonZero = true; break; }
            }
            if (anyNonZero) keep.Add(col);
        }
        group.Columns = keep;
    }

    private static void AssignColumn(IoRow row, IoColumn col, int value)
    {
        switch (col)
        {
            case IoColumn.Scan: row.Scan = value; break;
            case IoColumn.Logical: row.Logical = value; break;
            case IoColumn.Physical: row.Physical = value; break;
            case IoColumn.PageServer: row.PageServer = value; break;
            case IoColumn.ReadAhead: row.ReadAhead = value; break;
            case IoColumn.PageServerReadAhead: row.PageServerReadAhead = value; break;
            case IoColumn.LobLogical: row.LobLogical = value; break;
            case IoColumn.LobPhysical: row.LobPhysical = value; break;
            case IoColumn.LobPageServer: row.LobPageServer = value; break;
            case IoColumn.LobReadAhead: row.LobReadAhead = value; break;
            case IoColumn.LobPageServerReadAhead: row.LobPageServerReadAhead = value; break;
            case IoColumn.SegmentReads: row.SegmentReads = value; break;
            case IoColumn.SegmentSkipped: row.SegmentSkipped = value; break;
        }
    }

    private static int GetColumnValue(IoRow row, IoColumn col)
    {
        switch (col)
        {
            case IoColumn.Scan: return row.Scan;
            case IoColumn.Logical: return row.Logical;
            case IoColumn.Physical: return row.Physical;
            case IoColumn.PageServer: return row.PageServer;
            case IoColumn.ReadAhead: return row.ReadAhead;
            case IoColumn.PageServerReadAhead: return row.PageServerReadAhead;
            case IoColumn.LobLogical: return row.LobLogical;
            case IoColumn.LobPhysical: return row.LobPhysical;
            case IoColumn.LobPageServer: return row.LobPageServer;
            case IoColumn.LobReadAhead: return row.LobReadAhead;
            case IoColumn.LobPageServerReadAhead: return row.LobPageServerReadAhead;
            case IoColumn.SegmentReads: return row.SegmentReads;
            case IoColumn.SegmentSkipped: return row.SegmentSkipped;
            default: return 0;
        }
    }

    private static int GetColumnValue(IoGroupTotal entry, IoColumn col)
    {
        switch (col)
        {
            case IoColumn.Scan: return entry.Scan;
            case IoColumn.Logical: return entry.Logical;
            case IoColumn.Physical: return entry.Physical;
            case IoColumn.PageServer: return entry.PageServer;
            case IoColumn.ReadAhead: return entry.ReadAhead;
            case IoColumn.PageServerReadAhead: return entry.PageServerReadAhead;
            case IoColumn.LobLogical: return entry.LobLogical;
            case IoColumn.LobPhysical: return entry.LobPhysical;
            case IoColumn.LobPageServer: return entry.LobPageServer;
            case IoColumn.LobReadAhead: return entry.LobReadAhead;
            case IoColumn.LobPageServerReadAhead: return entry.LobPageServerReadAhead;
            case IoColumn.SegmentReads: return entry.SegmentReads;
            case IoColumn.SegmentSkipped: return entry.SegmentSkipped;
            default: return 0;
        }
    }

    private sealed class ParseContext
    {
        public ParserLanguage Lang = null!;
        public string[] Lines = Array.Empty<string>();
        public int LineIndex;
        public ParseResult Result = null!;
        public RowType PrevRowType = RowType.None;
        public IoGroup? CurrentGroup;
        public TimeTotal ExecutionTotal = null!;
        public TimeTotal CompileTotal = null!;
        public List<IoGroupTotal> GrandTotalData = null!;
        public List<IoColumn> GrandTotalColumns = null!;
        public int TableCount;
        public Regex TableNameRegex = null!;
    }
}
