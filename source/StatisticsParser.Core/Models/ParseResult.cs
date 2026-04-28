using System.Collections.Generic;

namespace StatisticsParser.Core.Models;

public class ParseResult
{
    public List<IResultRow> Data { get; set; } = new();
    public int TableCount { get; set; }
    public ParseResultTotal Total { get; set; } = new();
}
