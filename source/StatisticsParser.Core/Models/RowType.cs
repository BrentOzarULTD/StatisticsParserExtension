namespace StatisticsParser.Core.Models;

public enum RowType
{
    None,
    IO,
    ExecutionTime,
    CompileTime,
    RowsAffected,
    Error,
    IOTotal,
    ExecutionTimeTotal,
    CompileTimeTotal,
    Info,
    CompletionTime
}
