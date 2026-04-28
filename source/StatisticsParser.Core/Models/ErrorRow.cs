namespace StatisticsParser.Core.Models;

public class ErrorRow : IResultRow
{
    public RowType RowType => RowType.Error;
    public string Text { get; set; } = "";
}
