using System.Collections.Generic;

namespace StatisticsParser.Core.Models;

public class IoGrandTotal
{
    public List<IoColumn> Columns { get; set; } = new();
    public List<IoGroupTotal> Data { get; set; } = new();
    public IoGroupTotal Total { get; set; } = new();
}
