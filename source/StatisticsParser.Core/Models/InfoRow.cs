namespace StatisticsParser.Core.Models;

public class InfoRow : IResultRow
{
    public RowType RowType => RowType.Info;
    public string Text { get; set; } = "";
}
