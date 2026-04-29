using System;
using System.Linq;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class ParserTests
{
    private const string MultiBatchSample =
        "SQL Server parse and compile time: \n" +
        "   CPU time = 108 ms, elapsed time = 108 ms.\n" +
        "\n" +
        "(13431682 row(s) affected)\n" +
        "Table 'PostTypes'. Scan count 1, logical reads 2, physical reads 1, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Users'. Scan count 5, logical reads 42015, physical reads 1, read-ahead reads 41306, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Comments'. Scan count 5, logical reads 1089402, physical reads 248, read-ahead reads 1108174, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'PostTags'. Scan count 5, logical reads 77500, physical reads 348, read-ahead reads 82219, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Posts'. Scan count 5, logical reads 397944, physical reads 9338, read-ahead reads 402977, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Worktable'. Scan count 999172, logical reads 16247024, physical reads 0, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Worktable'. Scan count 0, logical reads 0, physical reads 0, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "\n" +
        " SQL Server Execution Times:\n" +
        "   CPU time = 156527 ms,  elapsed time = 284906 ms.\n" +
        "SQL Server parse and compile time: \n" +
        "   CPU time = 16 ms, elapsed time = 19 ms.\n" +
        "\n" +
        "(233033 row(s) affected)\n" +
        "Table 'Worktable'. Scan count 0, logical reads 0, physical reads 0, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Votes'. Scan count 1, logical reads 250128, physical reads 10, read-ahead reads 250104, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "Table 'Posts'. Scan count 1, logical reads 165586, physical reads 18, read-ahead reads 49191, lob logical reads 823463, lob physical reads 42854, lob read-ahead reads 3272.\n" +
        "Table 'Users'. Scan count 1, logical reads 41405, physical reads 3, read-ahead reads 41401, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.\n" +
        "\n" +
        " SQL Server Execution Times:\n" +
        "   CPU time = 17207 ms,  elapsed time = 38163 ms.\n" +
        "Msg 207, Level 16, State 1, Line 1\n" +
        "Invalid column name 'scores'.\n" +
        "SQL Server parse and compile time: \n" +
        "   CPU time = 0 ms, elapsed time = 0 ms.\n" +
        "\n" +
        " SQL Server Execution Times:\n" +
        "   CPU time = 0 ms,  elapsed time = 0 ms.\n" +
        "\n" +
        "Completion time: 2025-05-27T10:32:37.8122685-04:00\n";

    [Fact]
    public void ParseData_MultiBatchSample_ProducesTwoIoGroups()
    {
        var result = Parser.ParseData(MultiBatchSample);

        Assert.Equal(2, result.TableCount);

        var groups = result.Data.OfType<IoGroup>().ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal("resultTable_0", groups[0].TableId);
        Assert.Equal("resultTable_1", groups[1].TableId);
    }

    [Fact]
    public void ParseData_MultiBatchSample_FirstGroupHasExpectedRows()
    {
        var result = Parser.ParseData(MultiBatchSample);
        var group = result.Data.OfType<IoGroup>().First();

        Assert.Equal(7, group.Data.Count);

        var posts = group.Data.Single(r => r.TableName == "Posts");
        Assert.Equal(5, posts.Scan);
        Assert.Equal(397944, posts.Logical);
        Assert.Equal(9338, posts.Physical);
        Assert.Equal(402977, posts.ReadAhead);
        Assert.Equal(0, posts.LobLogical);

        var comments = group.Data.Single(r => r.TableName == "Comments");
        Assert.Equal(1089402, comments.Logical);

        Assert.Equal(2, group.Data.Count(r => r.TableName == "Worktable"));
    }

    [Fact]
    public void ParseData_MultiBatchSample_FirstGroupSuppressesAllZeroLobColumns()
    {
        var result = Parser.ParseData(MultiBatchSample);
        var group = result.Data.OfType<IoGroup>().First();

        Assert.Contains(IoColumn.Table, group.Columns);
        Assert.Contains(IoColumn.Scan, group.Columns);
        Assert.Contains(IoColumn.Logical, group.Columns);
        Assert.Contains(IoColumn.Physical, group.Columns);
        Assert.Contains(IoColumn.ReadAhead, group.Columns);
        Assert.Contains(IoColumn.PercentRead, group.Columns);

        Assert.DoesNotContain(IoColumn.LobLogical, group.Columns);
        Assert.DoesNotContain(IoColumn.LobPhysical, group.Columns);
        Assert.DoesNotContain(IoColumn.LobReadAhead, group.Columns);
    }

    [Fact]
    public void ParseData_MultiBatchSample_FirstGroupTotalsRollUp()
    {
        var result = Parser.ParseData(MultiBatchSample);
        var group = result.Data.OfType<IoGroup>().First();

        Assert.Equal(1 + 5 + 5 + 5 + 5 + 999172 + 0, group.Total.Scan);
        Assert.Equal(2 + 42015 + 1089402 + 77500 + 397944 + 16247024 + 0, group.Total.Logical);
        Assert.Equal(1 + 1 + 248 + 348 + 9338 + 0 + 0, group.Total.Physical);
        Assert.Equal(0 + 41306 + 1108174 + 82219 + 402977 + 0 + 0, group.Total.ReadAhead);
    }

    [Fact]
    public void ParseData_MultiBatchSample_FirstGroupAssignsPercentRead()
    {
        var result = Parser.ParseData(MultiBatchSample);
        var group = result.Data.OfType<IoGroup>().First();

        var worktable = group.Data.First(r => r.TableName == "Worktable" && r.Logical > 0);
        var expected = (16247024.0 / group.Total.Logical) * 100.0;
        Assert.Equal(expected, worktable.PercentRead, 6);
    }

    [Fact]
    public void ParseData_MultiBatchSample_SecondGroupKeepsLobColumns()
    {
        var result = Parser.ParseData(MultiBatchSample);
        var group = result.Data.OfType<IoGroup>().Skip(1).First();

        Assert.Equal(4, group.Data.Count);

        var posts = group.Data.Single(r => r.TableName == "Posts");
        Assert.Equal(823463, posts.LobLogical);
        Assert.Equal(42854, posts.LobPhysical);
        Assert.Equal(3272, posts.LobReadAhead);

        Assert.Contains(IoColumn.LobLogical, group.Columns);
        Assert.Contains(IoColumn.LobPhysical, group.Columns);
        Assert.Contains(IoColumn.LobReadAhead, group.Columns);
    }

    [Fact]
    public void ParseData_MultiBatchSample_TimeRowsAccumulateTotals()
    {
        var result = Parser.ParseData(MultiBatchSample);

        var compiles = result.Data.OfType<TimeRow>()
            .Where(r => r.RowType == RowType.CompileTime).ToList();
        var executions = result.Data.OfType<TimeRow>()
            .Where(r => r.RowType == RowType.ExecutionTime).ToList();

        Assert.Equal(3, compiles.Count);
        Assert.Equal(3, executions.Count);

        Assert.Equal(108, compiles[0].CpuMs);
        Assert.Equal(108, compiles[0].ElapsedMs);
        Assert.Equal(16, compiles[1].CpuMs);
        Assert.Equal(19, compiles[1].ElapsedMs);
        Assert.Equal(0, compiles[2].CpuMs);
        Assert.Equal(0, compiles[2].ElapsedMs);

        Assert.Equal(156527, executions[0].CpuMs);
        Assert.Equal(284906, executions[0].ElapsedMs);
        Assert.Equal(17207, executions[1].CpuMs);
        Assert.Equal(38163, executions[1].ElapsedMs);
        Assert.Equal(0, executions[2].CpuMs);
        Assert.Equal(0, executions[2].ElapsedMs);

        Assert.All(executions, r => Assert.False(r.Summary));

        Assert.Equal(108 + 16 + 0, result.Total.CompileTotal.CpuMs);
        Assert.Equal(108 + 19 + 0, result.Total.CompileTotal.ElapsedMs);
        Assert.Equal(156527 + 17207 + 0, result.Total.ExecutionTotal.CpuMs);
        Assert.Equal(284906 + 38163 + 0, result.Total.ExecutionTotal.ElapsedMs);
    }

    [Fact]
    public void ParseData_MultiBatchSample_RowsAffectedAreCaptured()
    {
        var result = Parser.ParseData(MultiBatchSample);

        var rowsAffected = result.Data.OfType<RowsAffectedRow>().ToList();
        Assert.Equal(2, rowsAffected.Count);
        Assert.Equal(13431682, rowsAffected[0].Count);
        Assert.Equal(233033, rowsAffected[1].Count);
    }

    [Fact]
    public void ParseData_MultiBatchSample_ErrorAndDetailAreCaptured()
    {
        var result = Parser.ParseData(MultiBatchSample);

        var errors = result.Data.OfType<ErrorRow>().ToList();
        Assert.Equal(2, errors.Count);
        Assert.Equal("Msg 207, Level 16, State 1, Line 1", errors[0].Text);
        Assert.Equal("Invalid column name 'scores'.", errors[1].Text);
    }

    [Fact]
    public void ParseData_MultiBatchSample_CompletionTimeIsParsed()
    {
        var result = Parser.ParseData(MultiBatchSample);

        var completion = Assert.Single(result.Data.OfType<CompletionTimeRow>());
        var expected = DateTimeOffset.Parse(
            "2025-05-27T10:32:37.8122685-04:00",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(expected, completion.Timestamp);
    }

    [Fact]
    public void ParseData_MultiBatchSample_DataRowsAreInExpectedOrder()
    {
        var result = Parser.ParseData(MultiBatchSample);

        var types = result.Data.Select(r => r.GetType()).ToArray();

        Assert.Equal(
            new[]
            {
                typeof(TimeRow),           // compile 108/108
                typeof(RowsAffectedRow),   // 13431682
                typeof(IoGroup),           // group 1
                typeof(TimeRow),           // execution 156527/284906
                typeof(TimeRow),           // compile 16/19
                typeof(RowsAffectedRow),   // 233033
                typeof(IoGroup),           // group 2
                typeof(TimeRow),           // execution 17207/38163
                typeof(ErrorRow),          // Msg 207...
                typeof(ErrorRow),          // Invalid column name
                typeof(TimeRow),           // compile 0/0
                typeof(TimeRow),           // execution 0/0
                typeof(CompletionTimeRow),
            },
            types);
    }

    [Fact]
    public void ParseData_MultiBatchSample_GrandTotalAggregatesAcrossGroups()
    {
        var result = Parser.ParseData(MultiBatchSample);
        var io = result.Total.IoTotal;

        var posts = io.Data.Single(d => d.TableName == "Posts");
        Assert.Equal(5 + 1, posts.Scan);
        Assert.Equal(397944 + 165586, posts.Logical);
        Assert.Equal(9338 + 18, posts.Physical);
        Assert.Equal(402977 + 49191, posts.ReadAhead);
        Assert.Equal(823463, posts.LobLogical);
        Assert.Equal(42854, posts.LobPhysical);
        Assert.Equal(3272, posts.LobReadAhead);

        var users = io.Data.Single(d => d.TableName == "Users");
        Assert.Equal(5 + 1, users.Scan);
        Assert.Equal(42015 + 41405, users.Logical);

        var worktable = io.Data.Single(d => d.TableName == "Worktable");
        Assert.Equal(999172, worktable.Scan);
        Assert.Equal(16247024, worktable.Logical);

        var tableNames = io.Data.Select(d => d.TableName).ToList();
        Assert.Equal(
            new[] { "Comments", "Posts", "PostTags", "PostTypes", "Users", "Votes", "Worktable" },
            tableNames);
    }

    private const string SingleStatementSample =
        "(100 rows affected)\n" +
        "Table 'Posts'. Scan count 1, logical reads 32, physical reads 3, page server reads 0, read-ahead reads 1957, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.\n" +
        "\n" +
        " SQL Server Execution Times:\n" +
        "   CPU time = 0 ms,  elapsed time = 959 ms.\n" +
        "\n" +
        "Completion time: 2026-04-27T15:33:34.6405733-04:00\n";

    private const string MultiStatementSample =
        "(100 rows affected)\n" +
        "Table 'Posts'. Scan count 1, logical reads 32, physical reads 3, page server reads 0, read-ahead reads 1957, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.\n" +
        "Table 'Users'. Scan count 1, logical reads 8, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.\n" +
        "\n" +
        " SQL Server Execution Times:\n" +
        "   CPU time = 5 ms,  elapsed time = 25 ms.\n" +
        "\n" +
        "Completion time: 2026-04-27T15:33:35.0000000-04:00\n" +
        "\n" +
        "(50 rows affected)\n" +
        "Table 'Comments'. Scan count 2, logical reads 64, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.\n" +
        "\n" +
        " SQL Server Execution Times:\n" +
        "   CPU time = 3 ms,  elapsed time = 15 ms.\n" +
        "\n" +
        "Completion time: 2026-04-27T15:33:35.3000000-04:00\n";

    [Fact]
    public void ParseData_SingleStatementSample_ProducesOneIoGroupWithPostsRow()
    {
        var result = Parser.ParseData(SingleStatementSample);

        Assert.Equal(1, result.TableCount);

        var group = Assert.Single(result.Data.OfType<IoGroup>());
        var posts = Assert.Single(group.Data);
        Assert.Equal("Posts", posts.TableName);
        Assert.Equal(1, posts.Scan);
        Assert.Equal(32, posts.Logical);
        Assert.Equal(3, posts.Physical);
        Assert.Equal(1957, posts.ReadAhead);
        Assert.Equal(32, group.Total.Logical);
        Assert.Equal(100.0, posts.PercentRead, 6);
    }

    [Fact]
    public void ParseData_SingleStatementSample_ExecutionTimeIsCapturedAndTotaled()
    {
        var result = Parser.ParseData(SingleStatementSample);

        var execution = Assert.Single(result.Data.OfType<TimeRow>(), r => r.RowType == RowType.ExecutionTime);
        Assert.False(execution.Summary);
        Assert.Equal(0, execution.CpuMs);
        Assert.Equal(959, execution.ElapsedMs);

        Assert.Equal(0, result.Total.ExecutionTotal.CpuMs);
        Assert.Equal(959, result.Total.ExecutionTotal.ElapsedMs);
    }

    [Fact]
    public void ParseData_MultiStatementSample_ProducesTwoIoGroupsWithCorrectPercentages()
    {
        var result = Parser.ParseData(MultiStatementSample);

        var groups = result.Data.OfType<IoGroup>().ToList();
        Assert.Equal(2, groups.Count);

        var group0 = groups[0];
        Assert.Equal(2, group0.Data.Count);
        var posts = group0.Data.Single(r => r.TableName == "Posts");
        var users = group0.Data.Single(r => r.TableName == "Users");
        Assert.Equal(40, group0.Total.Logical);
        Assert.Equal(80.0, posts.PercentRead, 3);
        Assert.Equal(20.0, users.PercentRead, 3);

        var group1 = groups[1];
        var comments = Assert.Single(group1.Data);
        Assert.Equal("Comments", comments.TableName);
        Assert.Equal(64, group1.Total.Logical);
        Assert.Equal(100.0, comments.PercentRead, 3);
    }

    [Fact]
    public void ParseData_MultiStatementSample_GrandTotalAggregatesAndSortsAlphabetically()
    {
        var result = Parser.ParseData(MultiStatementSample);
        var io = result.Total.IoTotal;

        Assert.Equal(
            new[] { "Comments", "Posts", "Users" },
            io.Data.Select(d => d.TableName).ToArray());

        Assert.Equal(104, io.Total.Logical);

        var comments = io.Data.Single(d => d.TableName == "Comments");
        var posts = io.Data.Single(d => d.TableName == "Posts");
        var users = io.Data.Single(d => d.TableName == "Users");
        Assert.Equal(61.538, comments.PercentRead, 3);
        Assert.Equal(30.769, posts.PercentRead, 3);
        Assert.Equal(7.692, users.PercentRead, 3);
    }

    [Fact]
    public void ParseData_MultiStatementSample_GrandTimeTotalSumsExecution()
    {
        var result = Parser.ParseData(MultiStatementSample);

        Assert.Equal(8, result.Total.ExecutionTotal.CpuMs);
        Assert.Equal(40, result.Total.ExecutionTotal.ElapsedMs);
        Assert.Equal(0, result.Total.CompileTotal.CpuMs);
        Assert.Equal(0, result.Total.CompileTotal.ElapsedMs);
    }

    [Fact]
    public void ParseData_EmptyInput_ReturnsEmptyResultWithoutThrowing()
    {
        var result = Parser.ParseData("");

        Assert.NotNull(result);
        Assert.Empty(result.Data);
        Assert.Equal(0, result.TableCount);
        Assert.Empty(result.Total.IoTotal.Data);
        Assert.Equal(0, result.Total.ExecutionTotal.CpuMs);
        Assert.Equal(0, result.Total.CompileTotal.CpuMs);
    }

    [Fact]
    public void ParseData_PlainProseInput_ProducesOnlyInfoRows()
    {
        var input = "Hello world\nThis isn't statistics output.";
        var result = Parser.ParseData(input);

        Assert.Equal(2, result.Data.Count);
        Assert.All(result.Data, row => Assert.IsType<InfoRow>(row));
        Assert.Equal("Hello world", ((InfoRow)result.Data[0]).Text);
        Assert.Equal("This isn't statistics output.", ((InfoRow)result.Data[1]).Text);
    }

    [Fact]
    public void ParseData_SegmentReadsContinuation_MergesIntoLastIoRow()
    {
        var input =
            "Table 'Foo'. scan count 1, logical reads 100, physical reads 0, read-ahead reads 0.\n" +
            "Table 'Foo'. scan count 0, logical reads 0, physical reads 0, read-ahead reads 0, segment reads 50, segment skipped 25.\n" +
            "\n" +
            " SQL Server Execution Times:\n" +
            "   CPU time = 1 ms,  elapsed time = 1 ms.\n";

        var result = Parser.ParseData(input);
        var group = Assert.Single(result.Data.OfType<IoGroup>());

        var foo = Assert.Single(group.Data);
        Assert.Equal("Foo", foo.TableName);
        Assert.Equal(100, foo.Logical);
        Assert.Equal(50, foo.SegmentReads);
        Assert.Equal(25, foo.SegmentSkipped);

        Assert.Contains(IoColumn.SegmentReads, group.Columns);
        Assert.Contains(IoColumn.SegmentSkipped, group.Columns);
    }

    [Theory]
    [InlineData(100, 200, 100, 202, true, 200)]
    [InlineData(100, 200, 100, 195, true, 200)]
    [InlineData(100, 200, 100, 206, false, 406)]
    [InlineData(100, 200, 99, 200, false, 400)]
    public void ParseData_SummaryExecutionTime_DetectionFollowsCpuExactAndElapsedTolerance(
        int firstCpu, int firstElapsed,
        int secondCpu, int secondElapsed,
        bool expectedSecondIsSummary,
        int expectedExecutionTotalElapsed)
    {
        var input =
            $" SQL Server Execution Times:\n" +
            $"   CPU time = {firstCpu} ms,  elapsed time = {firstElapsed} ms.\n" +
            $" SQL Server Execution Times:\n" +
            $"   CPU time = {secondCpu} ms,  elapsed time = {secondElapsed} ms.\n";

        var result = Parser.ParseData(input);
        var executions = result.Data.OfType<TimeRow>()
            .Where(r => r.RowType == RowType.ExecutionTime).ToList();

        Assert.Equal(2, executions.Count);
        Assert.False(executions[0].Summary);
        Assert.Equal(expectedSecondIsSummary, executions[1].Summary);
        Assert.Equal(expectedExecutionTotalElapsed, result.Total.ExecutionTotal.ElapsedMs);
    }
}
