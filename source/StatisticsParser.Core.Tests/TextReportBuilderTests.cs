using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using StatisticsParser.Core.Formatting;
using StatisticsParser.Core.Models;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class TextReportBuilderTests
{
    private const string Nl = "\r\n";
    private const string Tab = "\t";

    [Fact]
    public void Build_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextReportBuilder.Build(null));
    }

    [Fact]
    public void Build_EmptyData_ReturnsEmpty()
    {
        var result = new ParseResult();
        Assert.Equal(string.Empty, TextReportBuilder.Build(result));
    }

    [Fact]
    public void Build_RowsAffectedPlural_UsesGroupSeparator()
    {
        WithEnUs(() =>
        {
            var parsed = new ParseResult
            {
                Data = new List<IResultRow> { new RowsAffectedRow { Count = 1594 } },
            };
            var text = TextReportBuilder.Build(parsed);
            Assert.Contains("1,594 rows affected" + Nl, text);
        });
    }

    [Fact]
    public void Build_RowsAffectedSingular_UsesSingularNoun()
    {
        WithEnUs(() =>
        {
            var parsed = new ParseResult
            {
                Data = new List<IResultRow> { new RowsAffectedRow { Count = 1 } },
            };
            var text = TextReportBuilder.Build(parsed);
            Assert.Contains("1 row affected" + Nl, text);
            Assert.DoesNotContain("1 rows affected", text);
        });
    }

    [Fact]
    public void Build_ErrorRow_PreservesText()
    {
        var parsed = new ParseResult
        {
            Data = new List<IResultRow>
            {
                new ErrorRow { Text = "Msg 207, Level 16, State 1, Line 41 Invalid column name 'scores'." },
            },
        };
        var text = TextReportBuilder.Build(parsed);
        Assert.Contains("Msg 207, Level 16, State 1, Line 41 Invalid column name 'scores'.", text);
    }

    [Fact]
    public void Build_InfoRow_PreservesText()
    {
        var parsed = new ParseResult
        {
            Data = new List<IResultRow> { new InfoRow { Text = "Some informational note" } },
        };
        var text = TextReportBuilder.Build(parsed);
        Assert.Contains("Some informational note", text);
    }

    [Fact]
    public void Build_TotalsLabelAndGrandTimePresentAfterAnyData()
    {
        var parsed = new ParseResult
        {
            Data = new List<IResultRow> { new InfoRow { Text = "x" } },
            Total = new ParseResultTotal
            {
                CompileTotal = new TimeTotal { CpuMs = 47, ElapsedMs = 215 },
                ExecutionTotal = new TimeTotal { CpuMs = 118235, ElapsedMs = 190072 },
            },
        };
        var text = TextReportBuilder.Build(parsed);
        Assert.Contains("Totals:" + Nl, text);
        Assert.Contains("SQL Server parse and compile time" + Tab + "00:00:00.047" + Tab + "00:00:00.215", text);
        Assert.Contains("SQL Server Execution Times" + Tab + "00:01:58.235" + Tab + "00:03:10.072", text);
        Assert.Contains("Total" + Tab + "00:01:58.282" + Tab + "00:03:10.287", text);
    }

    [Fact]
    public void Build_TimeRows_EmitInlineCpuElapsedTable()
    {
        var parsed = new ParseResult
        {
            Data = new List<IResultRow>
            {
                new TimeRow { RowType = RowType.CompileTime, CpuMs = 47, ElapsedMs = 215, Summary = false },
                new TimeRow { RowType = RowType.ExecutionTime, CpuMs = 118235, ElapsedMs = 190072, Summary = false },
            },
        };
        var text = TextReportBuilder.Build(parsed);
        Assert.Contains(Tab + "CPU" + Tab + "Elapsed" + Nl, text);
        Assert.Contains("SQL Server parse and compile time" + Tab + "00:00:00.047" + Tab + "00:00:00.215" + Nl, text);
        Assert.Contains("SQL Server Execution Times" + Tab + "00:01:58.235" + Tab + "00:03:10.072" + Nl, text);
    }

    [Fact]
    public void Build_TimeSummaryRows_AreSkippedFromPerStatementTable()
    {
        // Summary=true rows are skipped from the per-statement Time table. Grand totals
        // still emit (CPU/Elapsed header for the Totals: section), so check that the
        // summary row's value (00:00:00.005) doesn't appear anywhere in the output.
        var parsed = new ParseResult
        {
            Data = new List<IResultRow>
            {
                new TimeRow { RowType = RowType.ExecutionTime, CpuMs = 5, ElapsedMs = 5, Summary = true },
            },
        };
        var text = TextReportBuilder.Build(parsed);
        Assert.DoesNotContain("00:00:00.005", text);
    }

    [Fact]
    public void Build_IoGroup_EmitsHeaderDataAndTotalRow()
    {
        WithEnUs(() =>
        {
            var group = new IoGroup
            {
                Columns = new List<IoColumn>
                {
                    IoColumn.Table, IoColumn.Scan, IoColumn.Logical, IoColumn.Physical,
                    IoColumn.ReadAhead, IoColumn.PercentRead,
                },
                Data = new List<IoRow>
                {
                    new() { TableName = "Posts", Scan = 9, Logical = 5_613_880, Physical = 5, ReadAhead = 5_583_511, PercentRead = 97.133 },
                    new() { TableName = "PostTags", Scan = 9, Logical = 95_994, Physical = 0, ReadAhead = 94_103, PercentRead = 1.661 },
                },
                Total = new IoGroupTotal
                {
                    Scan = 18, Logical = 5_709_874, Physical = 5, ReadAhead = 5_677_614,
                },
            };
            var parsed = new ParseResult { Data = new List<IResultRow> { group } };
            var text = TextReportBuilder.Build(parsed);

            // Header includes Row Num + Table + chosen metrics + percent column at the right.
            Assert.Contains(
                "Row Num" + Tab + "Table" + Tab + "Scan Count" + Tab + "Logical Reads" + Tab +
                "Physical Reads" + Tab + "Read-Ahead Reads" + Tab + "% Logical Reads of Total Reads" + Nl,
                text);

            // Data rows are 1-indexed; numbers use group separator; percent uses 3-decimal format.
            Assert.Contains(
                "1" + Tab + "Posts" + Tab + "9" + Tab + "5,613,880" + Tab + "5" + Tab + "5,583,511" + Tab + "97.133%" + Nl,
                text);
            Assert.Contains(
                "2" + Tab + "PostTags" + Tab + "9" + Tab + "95,994" + Tab + "0" + Tab + "94,103" + Tab + "1.661%" + Nl,
                text);

            // Total row leaves Row Num blank and the percent cell blank (trailing tab).
            Assert.Contains(
                Tab + "Total" + Tab + "18" + Tab + "5,709,874" + Tab + "5" + Tab + "5,677,614" + Tab + Nl,
                text);
        });
    }

    [Fact]
    public void Build_GrandIoTotal_EmitsTableHeaderWithoutRowNum()
    {
        WithEnUs(() =>
        {
            var parsed = new ParseResult
            {
                Data = new List<IResultRow> { new RowsAffectedRow { Count = 0 } },
                Total = new ParseResultTotal
                {
                    IoTotal = new IoGrandTotal
                    {
                        Columns = new List<IoColumn>
                        {
                            IoColumn.Table, IoColumn.Scan, IoColumn.Logical, IoColumn.PercentRead,
                        },
                        Data = new List<IoGroupTotal>
                        {
                            new() { TableName = "Posts", Scan = 18, Logical = 5_774_198, PercentRead = 92.378 },
                        },
                        Total = new IoGroupTotal { Scan = 18, Logical = 5_774_198 },
                    },
                },
            };
            var text = TextReportBuilder.Build(parsed);

            Assert.Contains("Totals:" + Nl, text);
            Assert.Contains("Table" + Tab + "Scan Count" + Tab + "Logical Reads" + Tab + "% Logical Reads of Total Reads" + Nl, text);
            Assert.Contains("Posts" + Tab + "18" + Tab + "5,774,198" + Tab + "92.378%" + Nl, text);
            // Grand total row: percent cell blank.
            Assert.Contains("Total" + Tab + "18" + Tab + "5,774,198" + Tab + Nl, text);
        });
    }

    [Fact]
    public void Build_UsesWindowsLineEndings()
    {
        var parsed = new ParseResult
        {
            Data = new List<IResultRow> { new InfoRow { Text = "hi" } },
        };
        var text = TextReportBuilder.Build(parsed);
        Assert.Contains(Nl, text);
        // No bare \n that isn't part of \r\n.
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                Assert.True(i > 0 && text[i - 1] == '\r', $"bare LF at index {i}");
        }
    }

    private static void WithEnUs(Action action)
    {
        var t = Thread.CurrentThread;
        var prevCulture = t.CurrentCulture;
        var prevUiCulture = t.CurrentUICulture;
        t.CurrentCulture = new CultureInfo("en-US");
        t.CurrentUICulture = new CultureInfo("en-US");
        try { action(); }
        finally
        {
            t.CurrentCulture = prevCulture;
            t.CurrentUICulture = prevUiCulture;
        }
    }
}
