using System.Collections.Generic;

namespace StatisticsParser.Core.Models;

public class IoGroup : IResultRow
{
    public RowType RowType => RowType.IO;

    public string TableId { get; set; } = "";
    public List<IoColumn> Columns { get; set; } = new();
    public List<IoRow> Data { get; set; } = new();
    public IoGroupTotal Total { get; set; } = new();
}
