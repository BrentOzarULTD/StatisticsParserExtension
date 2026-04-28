namespace StatisticsParser.Core.Models;

public enum IoColumn
{
    NotFound,
    Table,
    Scan,
    Logical,
    Physical,
    PageServer,
    ReadAhead,
    PageServerReadAhead,
    LobLogical,
    LobPhysical,
    LobPageServer,
    LobReadAhead,
    LobPageServerReadAhead,
    PercentRead,
    SegmentReads,
    SegmentSkipped
}
