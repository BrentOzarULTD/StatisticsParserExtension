namespace StatisticsParser.Core.Models;

public class TimeRow : IResultRow
{
    public RowType RowType { get; set; } = RowType.ExecutionTime;
    public int CpuMs { get; set; }
    public int ElapsedMs { get; set; }
    public bool Summary { get; set; }
}
