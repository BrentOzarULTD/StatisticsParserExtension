namespace StatisticsParser.Core.Models;

public class RowsAffectedRow : IResultRow
{
    public RowType RowType => RowType.RowsAffected;
    public int Count { get; set; }
}
