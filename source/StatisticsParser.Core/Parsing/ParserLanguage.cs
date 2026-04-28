using System;
using System.Collections.Generic;
using StatisticsParser.Core.Models;

namespace StatisticsParser.Core.Parsing;

public sealed class ParserLanguage
{
    public string LangValue { get; set; } = "";
    public string LangName { get; set; } = "";

    public string Table { get; set; } = "";
    public string ExecutionTime { get; set; } = "";
    public string CompileTime { get; set; } = "";
    public string CpuTime { get; set; } = "";
    public string ElapsedTime { get; set; } = "";
    public string Milliseconds { get; set; } = "";
    public IReadOnlyList<string> RowsAffected { get; set; } = Array.Empty<string>();
    public string ErrorMsg { get; set; } = "";
    public string CompletionTimeLabel { get; set; } = "";

    public IReadOnlyList<string> Scan { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Logical { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Physical { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PageServer { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ReadAhead { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PageServerReadAhead { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LobLogical { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LobPhysical { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LobPageServer { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LobReadAhead { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LobPageServerReadAhead { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SegmentReads { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SegmentSkipped { get; set; } = Array.Empty<string>();

    public static ParserLanguage English { get; } = new ParserLanguage
    {
        LangValue = "en",
        LangName = "English",
        Table = "Table",
        ExecutionTime = "SQL Server Execution Times:",
        CompileTime = "SQL Server parse and compile time:",
        CpuTime = "CPU time = ",
        ElapsedTime = "elapsed time = ",
        Milliseconds = "ms",
        RowsAffected = new[] { "row(s) affected", "row affected", "rows affected" },
        ErrorMsg = "Msg",
        CompletionTimeLabel = "Completion time: ",
        Scan = new[] { "scan count" },
        Logical = new[] { "logical reads" },
        Physical = new[] { "physical reads" },
        PageServer = new[] { "page server reads" },
        ReadAhead = new[] { "read-ahead reads" },
        PageServerReadAhead = new[] { "page server read-ahead reads" },
        LobLogical = new[] { "lob logical reads", "lob logical reads" },
        LobPhysical = new[] { "lob physical reads" },
        LobPageServer = new[] { "lob page server reads" },
        LobReadAhead = new[] { "lob read-ahead reads" },
        LobPageServerReadAhead = new[] { "lob page server read-ahead reads" },
        SegmentReads = new[] { "segment reads" },
        SegmentSkipped = new[] { "segment skipped" }
    };

    public static ParserLanguage Spanish { get; } = new ParserLanguage
    {
        LangValue = "es",
        LangName = "Español",
        Table = "Tabla",
        ExecutionTime = "Tiempos de ejecución de SQL Server:",
        CompileTime = "Tiempo de análisis y compilación de SQL Server:",
        CpuTime = "Tiempo de CPU = ",
        ElapsedTime = "tiempo transcurrido = ",
        Milliseconds = "ms",
        RowsAffected = new[] { "filas afectadas", "fila afectada" },
        ErrorMsg = "Msg",
        CompletionTimeLabel = "Completion time: ",
        Scan = new[] { "recuento de exámenes", "número de examen" },
        Logical = new[] { "lecturas lógicas" },
        Physical = new[] { "lecturas físicas" },
        PageServer = new[] { "lecturas de servidor de páginas" },
        ReadAhead = new[] { "lecturas anticipadas" },
        PageServerReadAhead = new[] { "lecturas anticipadas de servidor de páginas" },
        LobLogical = new[] { "lecturas lógicas de lob", "lecturas lógicas de línea de negocio" },
        LobPhysical = new[] { "lecturas físicas de lob", "lecturas físicas de línea de negocio" },
        LobPageServer = new[] { "lecturas de servidor de páginas de línea de negocio" },
        LobReadAhead = new[] { "lecturas anticipadas de lob", "lecturas anticipadas de línea de negocio" },
        LobPageServerReadAhead = new[] { "lecturas anticipadas de servidor de páginas de línea de negocio" },
        SegmentReads = new[] { "lecturas de segmento" },
        SegmentSkipped = new[] { "segmento saltado" }
    };

    public static ParserLanguage Italian { get; } = new ParserLanguage
    {
        LangValue = "it",
        LangName = "Italian",
        Table = "Tabella",
        ExecutionTime = "Tempo di esecuzione SQL Server:",
        CompileTime = "Tempo di analisi e compilazione SQL Server:",
        CpuTime = "tempo di CPU = ",
        ElapsedTime = "tempo trascorso = ",
        Milliseconds = "ms",
        RowsAffected = new[] { "righe interessate", "riga interessata" },
        ErrorMsg = "Mes",
        CompletionTimeLabel = "Completion time: ",
        Scan = new[] { "conteggio analisi" },
        Logical = new[] { "letture logiche" },
        Physical = new[] { "letture fisiche" },
        PageServer = new[] { "letture server di pagine" },
        ReadAhead = new[] { "letture read-ahead" },
        PageServerReadAhead = new[] { "letture read-ahead server di pagine" },
        LobLogical = new[] { "letture logiche lob" },
        LobPhysical = new[] { "letture fisiche lob" },
        LobPageServer = new[] { "letture lob server di pagine" },
        LobReadAhead = new[] { "letture lob read-ahead" },
        LobPageServerReadAhead = new[] { "letture read-ahead lob server di pagine" },
        SegmentReads = new[] { "letture segmento" },
        SegmentSkipped = new[] { "segmento saltato" }
    };

    public static IReadOnlyList<ParserLanguage> All { get; } = new[] { English, Spanish, Italian };

    public IoColumn DetermineIoColumn(string columnText)
    {
        var trimmed = columnText.Trim();

        if (Match(Scan)) return IoColumn.Scan;
        if (Match(Logical)) return IoColumn.Logical;
        if (Match(Physical)) return IoColumn.Physical;
        if (Match(PageServer)) return IoColumn.PageServer;
        if (Match(ReadAhead)) return IoColumn.ReadAhead;
        if (Match(PageServerReadAhead)) return IoColumn.PageServerReadAhead;
        if (Match(LobLogical)) return IoColumn.LobLogical;
        if (Match(LobPhysical)) return IoColumn.LobPhysical;
        if (Match(LobPageServer)) return IoColumn.LobPageServer;
        if (Match(LobReadAhead)) return IoColumn.LobReadAhead;
        if (Match(LobPageServerReadAhead)) return IoColumn.LobPageServerReadAhead;
        if (Match(SegmentReads)) return IoColumn.SegmentReads;
        if (Match(SegmentSkipped)) return IoColumn.SegmentSkipped;
        return IoColumn.NotFound;

        bool Match(IReadOnlyList<string> variants)
        {
            for (int i = 0; i < variants.Count; i++)
            {
                if (string.Equals(trimmed, variants[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
