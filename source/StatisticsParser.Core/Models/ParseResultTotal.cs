namespace StatisticsParser.Core.Models;

public class ParseResultTotal
{
    public TimeTotal ExecutionTotal { get; set; } = new() { RowType = RowType.ExecutionTimeTotal };
    public TimeTotal CompileTotal { get; set; } = new() { RowType = RowType.CompileTimeTotal };
    public IoGrandTotal IoTotal { get; set; } = new();
}
