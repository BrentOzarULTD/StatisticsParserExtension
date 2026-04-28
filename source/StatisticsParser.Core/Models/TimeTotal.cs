namespace StatisticsParser.Core.Models;

public class TimeTotal : IResultRow
{
    public RowType RowType { get; set; } = RowType.ExecutionTimeTotal;
    public int CpuMs { get; set; }
    public int ElapsedMs { get; set; }
}
