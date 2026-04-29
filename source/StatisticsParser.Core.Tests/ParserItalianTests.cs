using System.Linq;
using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class ParserItalianTests
{
    private const string ItalianLargeBatchSample =
        "Tempo di analisi e compilazione SQL Server: \n" +
        "   tempo di CPU = 0 ms, tempo trascorso = 0 ms.\n" +
        "\n" +
        "(25499 righe interessate)\n" +
        "Tabella 'Workfile'. Conteggio analisi 0, letture logiche 0, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'Worktable'. Conteggio analisi 0, letture logiche 0, letture fisiche 0, letture server di pagine 0, letture read-ahead 856, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'FATRIG'. Conteggio analisi 1, letture logiche 13563, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'FATTES'. Conteggio analisi 1, letture logiche 3748, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'COMRIG'. Conteggio analisi 1, letture logiche 170, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'CONTI'. Conteggio analisi 1, letture logiche 224, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'TIPIDOC'. Conteggio analisi 2, letture logiche 8, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahea LOB server di pagine 0.\n" +
        "Tabella 'COMTES'. Conteggio analisi 1, letture logiche 1702, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'CLIENTI'. Conteggio analisi 2, letture logiche 20, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'PERSONE'. Conteggio analisi 4, letture logiche 794, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'ANAPAG'. Conteggio analisi 1, letture logiche 5, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'NAZIONI'. Conteggio analisi 1, letture logiche 2, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'AGENTI'. Conteggio analisi 2, letture logiche 4, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'LOTSER'. Conteggio analisi 1, letture logiche 18, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'ARTICO'. Conteggio analisi 1, letture logiche 370, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'CLASSI'. Conteggio analisi 1, letture logiche 2, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'CATOMO'. Conteggio analisi 1, letture logiche 2, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'GRUPPI'. Conteggio analisi 1, letture logiche 2, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'LINEEP'. Conteggio analisi 1, letture logiche 2, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "Tabella 'MARCHE'. Conteggio analisi 1, letture logiche 4, letture fisiche 0, letture server di pagine 0, letture read-ahead 0, letture read-ahead server di pagine 0, letture logiche LOB 0, letture fisiche LOB 0, letture LOB server di pagine 0, letture LOB read-ahead 0, letture read-ahead LOB server di pagine 0.\n" +
        "\n" +
        "Tempo di esecuzione SQL Server: \n" +
        " tempo di CPU = 657 ms, tempo trascorso = 944 ms.\n" +
        "\n" +
        "Messaggio 207, livello 16, stato 1, riga 1\n" +
        "Il nome di colonna 'test' non è valido.\n";

    [Fact]
    public void ParseData_ItalianSample_ProducesOneIoGroupWithTwentyRows()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);

        Assert.Equal(1, result.TableCount);
        var group = Assert.Single(result.Data.OfType<IoGroup>());
        Assert.Equal("resultTable_0", group.TableId);
        Assert.Equal(20, group.Data.Count);
    }

    [Fact]
    public void ParseData_ItalianSample_GroupHasExpectedSpotCheckValues()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);
        var group = result.Data.OfType<IoGroup>().Single();

        Assert.Equal(13563, group.Data.Single(r => r.TableName == "FATRIG").Logical);
        Assert.Equal(3748, group.Data.Single(r => r.TableName == "FATTES").Logical);
        Assert.Equal(0, group.Data.Single(r => r.TableName == "Workfile").Scan);
        Assert.Equal(856, group.Data.Single(r => r.TableName == "Worktable").ReadAhead);
        Assert.Equal(1, group.Data.Single(r => r.TableName == "MARCHE").Scan);
        Assert.Equal(2, group.Data.Single(r => r.TableName == "TIPIDOC").Scan);
    }

    [Fact]
    public void ParseData_ItalianSample_GroupTotalLogicalReads()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);
        var group = result.Data.OfType<IoGroup>().Single();

        var expectedLogical =
            0 + 0 + 13563 + 3748 + 170 + 224 + 8 + 1702 + 20 + 794
            + 5 + 2 + 4 + 18 + 370 + 2 + 2 + 2 + 2 + 4;
        Assert.Equal(20_640, expectedLogical);
        Assert.Equal(expectedLogical, group.Total.Logical);

        var expectedScan = 0 + 0 + 1 + 1 + 1 + 1 + 2 + 1 + 2 + 4 + 1 + 1 + 2 + 1 + 1 + 1 + 1 + 1 + 1 + 1;
        Assert.Equal(24, expectedScan);
        Assert.Equal(expectedScan, group.Total.Scan);
        Assert.Equal(856, group.Total.ReadAhead);
    }

    [Fact]
    public void ParseData_ItalianSample_SuppressesZeroOnlyColumns()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);
        var group = result.Data.OfType<IoGroup>().Single();

        Assert.Contains(IoColumn.Table, group.Columns);
        Assert.Contains(IoColumn.Scan, group.Columns);
        Assert.Contains(IoColumn.Logical, group.Columns);
        Assert.Contains(IoColumn.ReadAhead, group.Columns);
        Assert.Contains(IoColumn.PercentRead, group.Columns);

        Assert.DoesNotContain(IoColumn.Physical, group.Columns);
        Assert.DoesNotContain(IoColumn.PageServer, group.Columns);
        Assert.DoesNotContain(IoColumn.PageServerReadAhead, group.Columns);
        Assert.DoesNotContain(IoColumn.LobLogical, group.Columns);
        Assert.DoesNotContain(IoColumn.LobPhysical, group.Columns);
        Assert.DoesNotContain(IoColumn.LobPageServer, group.Columns);
        Assert.DoesNotContain(IoColumn.LobReadAhead, group.Columns);
        Assert.DoesNotContain(IoColumn.LobPageServerReadAhead, group.Columns);
    }

    [Fact]
    public void ParseData_ItalianSample_TimesAndRowsAffectedCaptured()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);

        var rowsAffected = Assert.Single(result.Data.OfType<RowsAffectedRow>());
        Assert.Equal(25499, rowsAffected.Count);

        var compile = Assert.Single(result.Data.OfType<TimeRow>(), r => r.RowType == RowType.CompileTime);
        Assert.Equal(0, compile.CpuMs);
        Assert.Equal(0, compile.ElapsedMs);

        var execution = Assert.Single(result.Data.OfType<TimeRow>(), r => r.RowType == RowType.ExecutionTime);
        Assert.Equal(657, execution.CpuMs);
        Assert.Equal(944, execution.ElapsedMs);
        Assert.False(execution.Summary);

        Assert.Equal(0, result.Total.CompileTotal.CpuMs);
        Assert.Equal(0, result.Total.CompileTotal.ElapsedMs);
        Assert.Equal(657, result.Total.ExecutionTotal.CpuMs);
        Assert.Equal(944, result.Total.ExecutionTotal.ElapsedMs);
    }

    // The Italian "Messaggio" line is misclassified as InfoRow because the parser
    // requires the character after lang.ErrorMsg ("Mes") to be a space or end-of-line,
    // and "Messaggio"[3] is 's'. This test pins that current behavior; if the parser
    // is later updated to recognize the full Italian "Messaggio" token, update this
    // test to expect ErrorRow instead.
    [Fact]
    public void ParseData_ItalianSample_MessaggioErrorIsCurrentlyInfoRow()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);

        Assert.Empty(result.Data.OfType<ErrorRow>());

        var info = result.Data.OfType<InfoRow>().Select(r => r.Text).ToList();
        Assert.Contains("Messaggio 207, livello 16, stato 1, riga 1", info);
        Assert.Contains("Il nome di colonna 'test' non è valido.", info);
    }

    [Fact]
    public void ParseData_ItalianSample_GrandTotalEqualsSingleGroupTotal()
    {
        var result = Parser.ParseData(ItalianLargeBatchSample, ParserLanguage.Italian);
        var io = result.Total.IoTotal;

        Assert.Equal(20, io.Data.Count);

        var sortedNames = io.Data.Select(d => d.TableName).ToArray();
        var expectedSorted = sortedNames.OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(expectedSorted, sortedNames);

        Assert.Equal(20_640, io.Total.Logical);
    }
}
