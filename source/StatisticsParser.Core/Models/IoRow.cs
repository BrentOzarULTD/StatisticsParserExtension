namespace StatisticsParser.Core.Models;

public class IoRow : IResultRow
{
    public RowType RowType => RowType.IO;

    public string TableName { get; set; } = "";

    public int Scan { get; set; }
    public int Logical { get; set; }
    public int Physical { get; set; }
    public int PageServer { get; set; }
    public int ReadAhead { get; set; }
    public int PageServerReadAhead { get; set; }
    public int LobLogical { get; set; }
    public int LobPhysical { get; set; }
    public int LobPageServer { get; set; }
    public int LobReadAhead { get; set; }
    public int LobPageServerReadAhead { get; set; }
    public int SegmentReads { get; set; }
    public int SegmentSkipped { get; set; }

    public double PercentRead { get; set; }
}
