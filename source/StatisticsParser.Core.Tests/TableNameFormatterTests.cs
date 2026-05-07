using StatisticsParser.Core.Formatting;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class TableNameFormatterTests
{
    [Fact]
    public void FormatForDisplay_TempTableWithLongUnderscoreRun_CollapsesToEllipsis()
    {
        // SQL Server-generated temp table from the user's sample output: "#Orders" + 113 underscores + 12-digit suffix.
        var name = "#Orders" + new string('_', 113) + "000000000157";
        var result = TableNameFormatter.FormatForDisplay(name);
        Assert.Equal("#Orders__…__000000000157", result);
    }

    [Fact]
    public void FormatForDisplay_TempTableWithSevenUnderscores_Truncated()
    {
        Assert.Equal("#a__…__b", TableNameFormatter.FormatForDisplay("#a_______b"));
    }

    [Fact]
    public void FormatForDisplay_TempTableWithSixUnderscores_Unchanged()
    {
        var name = "#a______b";
        Assert.Equal(name, TableNameFormatter.FormatForDisplay(name));
    }

    [Fact]
    public void FormatForDisplay_TempTableWithoutUnderscores_Unchanged()
    {
        var name = "#TempOrders";
        Assert.Equal(name, TableNameFormatter.FormatForDisplay(name));
    }

    [Fact]
    public void FormatForDisplay_NonTempTableWithLongUnderscoreRun_Unchanged()
    {
        var name = "Orders" + new string('_', 50) + "X";
        Assert.Equal(name, TableNameFormatter.FormatForDisplay(name));
    }

    [Fact]
    public void FormatForDisplay_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TableNameFormatter.FormatForDisplay(string.Empty));
    }

    [Fact]
    public void FormatForDisplay_MultipleSeparateRuns_EachCollapsedIndependently()
    {
        Assert.Equal("#a__…__b__…__c", TableNameFormatter.FormatForDisplay("#a_______b_______c"));
    }

    [Fact]
    public void IsTruncated_TempTableWithLongUnderscoreRun_True()
    {
        Assert.True(TableNameFormatter.IsTruncated("#a_______b"));
    }

    [Fact]
    public void IsTruncated_TempTableWithSixUnderscores_False()
    {
        Assert.False(TableNameFormatter.IsTruncated("#a______b"));
    }

    [Fact]
    public void IsTruncated_NonTempTableWithLongUnderscoreRun_False()
    {
        Assert.False(TableNameFormatter.IsTruncated("a_______b"));
    }

    [Fact]
    public void IsTruncated_Empty_False()
    {
        Assert.False(TableNameFormatter.IsTruncated(string.Empty));
    }
}
