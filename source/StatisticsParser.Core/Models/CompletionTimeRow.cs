using System;

namespace StatisticsParser.Core.Models;

public class CompletionTimeRow : IResultRow
{
    public RowType RowType => RowType.CompletionTime;
    public DateTimeOffset Timestamp { get; set; }
}
