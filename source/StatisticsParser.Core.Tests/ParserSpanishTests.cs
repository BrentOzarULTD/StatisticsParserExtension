using System;
using System.Globalization;
using System.Linq;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class ParserSpanishTests
{
    private const string SpanishMultiBatchSample =
        "Tiempo de análisis y compilación de SQL Server: \n" +
        "   Tiempo de CPU = 135 ms, tiempo transcurrido = 135 ms.\n" +
        "\n" +
        "(13431682 filas afectadas)\n" +
        "Tabla 'PostTypes'. Recuento de exámenes 1, lecturas lógicas 2, lecturas físicas 1, lecturas anticipadas 0, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Users'. Recuento de exámenes 5, lecturas lógicas 42015, lecturas físicas 1, lecturas anticipadas 41305, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Comments'. Recuento de exámenes 5, lecturas lógicas 1089147, lecturas físicas 19, lecturas anticipadas 1088411, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'PostTags'. Recuento de exámenes 5, lecturas lógicas 77870, lecturas físicas 3, lecturas anticipadas 76763, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Posts'. Recuento de exámenes 5, lecturas lógicas 396629, lecturas físicas 26, lecturas anticipadas 394952, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Worktable'. Recuento de exámenes 999172, lecturas lógicas 16247024, lecturas físicas 0, lecturas anticipadas 0, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Worktable'. Recuento de exámenes 0, lecturas lógicas 0, lecturas físicas 0, lecturas anticipadas 0, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "\n" +
        " Tiempos de ejecución de SQL Server:\n" +
        "   Tiempo de CPU = 164456 ms, tiempo transcurrido = 293219 ms.\n" +
        "Tiempo de análisis y compilación de SQL Server: \n" +
        "   Tiempo de CPU = 24 ms, tiempo transcurrido = 24 ms.\n" +
        "\n" +
        "(233033 filas afectadas)\n" +
        "Tabla 'Worktable'. Recuento de exámenes 0, lecturas lógicas 0, lecturas físicas 0, lecturas anticipadas 0, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Votes'. Recuento de exámenes 1, lecturas lógicas 250128, lecturas físicas 4, lecturas anticipadas 250123, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "Tabla 'Posts'. Recuento de exámenes 1, lecturas lógicas 161111, lecturas físicas 22, lecturas anticipadas 53658, lecturas lógicas de LOB 823412, lecturas físicas de LOB 42463, lecturas anticipadas de LOB 3272.\n" +
        "Tabla 'Users'. Recuento de exámenes 1, lecturas lógicas 41405, lecturas físicas 0, lecturas anticipadas 41231, lecturas lógicas de LOB 0, lecturas físicas de LOB 0, lecturas anticipadas de LOB 0.\n" +
        "\n" +
        " Tiempos de ejecución de SQL Server:\n" +
        "   Tiempo de CPU = 17847 ms, tiempo transcurrido = 36306 ms.\n" +
        "Msg 207, Level 16, State 1, Line 1\n" +
        "El nombre de columna 'scores' no es válido.\n" +
        "Tiempo de análisis y compilación de SQL Server: \n" +
        "   Tiempo de CPU = 0 ms, tiempo transcurrido = 0 ms.\n" +
        "\n" +
        " Tiempos de ejecución de SQL Server:\n" +
        "   Tiempo de CPU = 0 ms, tiempo transcurrido = 0 ms.\n" +
        "Completion time: 2025-05-27T10:32:37.8122685-04:00\n";

    [Fact]
    public void ParseData_SpanishSample_ProducesTwoIoGroups()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);

        Assert.Equal(2, result.TableCount);

        var groups = result.Data.OfType<IoGroup>().ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal("resultTable_0", groups[0].TableId);
        Assert.Equal("resultTable_1", groups[1].TableId);
    }

    [Fact]
    public void ParseData_SpanishSample_FirstGroupHasSevenRowsWithExpectedValues()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);
        var group = result.Data.OfType<IoGroup>().First();

        Assert.Equal(7, group.Data.Count);

        var comments = group.Data.Single(r => r.TableName == "Comments");
        Assert.Equal(1089147, comments.Logical);

        var posts = group.Data.Single(r => r.TableName == "Posts");
        Assert.Equal(396629, posts.Logical);
        Assert.Equal(394952, posts.ReadAhead);

        var worktables = group.Data.Where(r => r.TableName == "Worktable").ToList();
        Assert.Equal(2, worktables.Count);
        Assert.Equal(999172, worktables.Sum(r => r.Scan));
        Assert.Equal(16247024, worktables.Sum(r => r.Logical));
    }

    [Fact]
    public void ParseData_SpanishSample_FirstGroupTotalsRollUp()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);
        var group = result.Data.OfType<IoGroup>().First();

        Assert.Equal(1 + 5 + 5 + 5 + 5 + 999172 + 0, group.Total.Scan);
        Assert.Equal(2 + 42015 + 1089147 + 77870 + 396629 + 16247024 + 0, group.Total.Logical);
        Assert.Equal(1 + 1 + 19 + 3 + 26 + 0 + 0, group.Total.Physical);
        Assert.Equal(0 + 41305 + 1088411 + 76763 + 394952 + 0 + 0, group.Total.ReadAhead);
    }

    [Fact]
    public void ParseData_SpanishSample_FirstGroupSuppressesAllZeroLobColumns()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);
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
    public void ParseData_SpanishSample_SecondGroupKeepsLobColumnsForPosts()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);
        var group = result.Data.OfType<IoGroup>().Skip(1).First();

        Assert.Equal(4, group.Data.Count);

        var posts = group.Data.Single(r => r.TableName == "Posts");
        Assert.Equal(823412, posts.LobLogical);
        Assert.Equal(42463, posts.LobPhysical);
        Assert.Equal(3272, posts.LobReadAhead);

        Assert.Contains(IoColumn.LobLogical, group.Columns);
        Assert.Contains(IoColumn.LobPhysical, group.Columns);
        Assert.Contains(IoColumn.LobReadAhead, group.Columns);
    }

    [Fact]
    public void ParseData_SpanishSample_TimeRowsAccumulateTotals()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);

        var compiles = result.Data.OfType<TimeRow>()
            .Where(r => r.RowType == RowType.CompileTime).ToList();
        var executions = result.Data.OfType<TimeRow>()
            .Where(r => r.RowType == RowType.ExecutionTime).ToList();

        Assert.Equal(3, compiles.Count);
        Assert.Equal(3, executions.Count);

        Assert.Equal(135, compiles[0].CpuMs);
        Assert.Equal(135, compiles[0].ElapsedMs);
        Assert.Equal(24, compiles[1].CpuMs);
        Assert.Equal(24, compiles[1].ElapsedMs);
        Assert.Equal(0, compiles[2].CpuMs);
        Assert.Equal(0, compiles[2].ElapsedMs);

        Assert.Equal(164456, executions[0].CpuMs);
        Assert.Equal(293219, executions[0].ElapsedMs);
        Assert.Equal(17847, executions[1].CpuMs);
        Assert.Equal(36306, executions[1].ElapsedMs);
        Assert.Equal(0, executions[2].CpuMs);
        Assert.Equal(0, executions[2].ElapsedMs);

        Assert.All(executions, r => Assert.False(r.Summary));

        Assert.Equal(135 + 24 + 0, result.Total.CompileTotal.CpuMs);
        Assert.Equal(135 + 24 + 0, result.Total.CompileTotal.ElapsedMs);
        Assert.Equal(164456 + 17847 + 0, result.Total.ExecutionTotal.CpuMs);
        Assert.Equal(293219 + 36306 + 0, result.Total.ExecutionTotal.ElapsedMs);
    }

    [Fact]
    public void ParseData_SpanishSample_RowsAffectedAndErrorAreCaptured()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);

        var rowsAffected = result.Data.OfType<RowsAffectedRow>().ToList();
        Assert.Equal(2, rowsAffected.Count);
        Assert.Equal(13431682, rowsAffected[0].Count);
        Assert.Equal(233033, rowsAffected[1].Count);

        var errors = result.Data.OfType<ErrorRow>().ToList();
        Assert.Equal(2, errors.Count);
        Assert.Equal("Msg 207, Level 16, State 1, Line 1", errors[0].Text);
        Assert.Equal("El nombre de columna 'scores' no es válido.", errors[1].Text);
    }

    [Fact]
    public void ParseData_SpanishSample_CompletionTimeIsParsed()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);

        var completion = Assert.Single(result.Data.OfType<CompletionTimeRow>());
        var expected = DateTimeOffset.Parse(
            "2025-05-27T10:32:37.8122685-04:00",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal);
        Assert.Equal(expected, completion.Timestamp);
        Assert.Equal(TimeSpan.FromHours(-4), completion.Timestamp.Offset);
    }

    [Fact]
    public void ParseData_SpanishSample_GrandTotalAggregatesAndSortsAlphabetically()
    {
        var result = Parser.ParseData(SpanishMultiBatchSample, ParserLanguage.Spanish);
        var io = result.Total.IoTotal;

        Assert.Equal(
            new[] { "Comments", "Posts", "PostTags", "PostTypes", "Users", "Votes", "Worktable" },
            io.Data.Select(d => d.TableName).ToArray());

        var posts = io.Data.Single(d => d.TableName == "Posts");
        Assert.Equal(396629 + 161111, posts.Logical);
        Assert.Equal(823412, posts.LobLogical);
        Assert.Equal(42463, posts.LobPhysical);
        Assert.Equal(3272, posts.LobReadAhead);

        var users = io.Data.Single(d => d.TableName == "Users");
        Assert.Equal(42015 + 41405, users.Logical);

        var worktable = io.Data.Single(d => d.TableName == "Worktable");
        Assert.Equal(16247024, worktable.Logical);

        Assert.Equal(18_305_331, io.Total.Logical);
    }
}
