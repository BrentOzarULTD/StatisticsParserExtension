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

    [Fact]
    public void StripGeneratedSuffix_TempTableWithSqlServerPadding_ReturnsOriginalName()
    {
        // Same fixture as FormatForDisplay test: "#Orders" + 113 underscores + 12-digit suffix.
        var name = "#Orders" + new string('_', 113) + "000000000157";
        Assert.Equal("#Orders", TableNameFormatter.StripGeneratedSuffix(name));
    }

    [Fact]
    public void StripGeneratedSuffix_HexSuffixWithLetters_ReturnsOriginalName()
    {
        // Real SQL Server output: 12-char hex suffix can include A-F.
        var name = "#Orders" + new string('_', 113) + "0000000001DC";
        Assert.Equal("#Orders", TableNameFormatter.StripGeneratedSuffix(name));
    }

    [Fact]
    public void HasGeneratedSuffix_HexSuffixWithLetters_True()
    {
        var name = "#Orders" + new string('_', 113) + "0000000001DC";
        Assert.True(TableNameFormatter.HasGeneratedSuffix(name));
    }

    [Fact]
    public void StripGeneratedSuffix_NamePreservesInternalUnderscores()
    {
        // Underscores inside the user-written name must survive; only the trailing
        // padding+12-digit signature is stripped.
        Assert.Equal("#Foo_123", TableNameFormatter.StripGeneratedSuffix("#Foo_123_____000000000157"));
    }

    [Fact]
    public void StripGeneratedSuffix_TempTableWithoutSuffix_Unchanged()
    {
        Assert.Equal("#Foo", TableNameFormatter.StripGeneratedSuffix("#Foo"));
    }

    [Fact]
    public void StripGeneratedSuffix_GlobalTempTable_Unchanged()
    {
        // '##' globals don't get the session-suffix padding from SQL Server, so they
        // must pass through unchanged regardless of any underscore+digit tail.
        Assert.Equal("##GlobalTemp", TableNameFormatter.StripGeneratedSuffix("##GlobalTemp"));
    }

    [Fact]
    public void StripGeneratedSuffix_NonTempTable_Unchanged()
    {
        var name = "Orders" + new string('_', 5) + "000000000157";
        Assert.Equal(name, TableNameFormatter.StripGeneratedSuffix(name));
    }

    [Fact]
    public void StripGeneratedSuffix_TrailingDigitsButNotTwelve_Unchanged()
    {
        // Only 3 trailing digits — not the SQL Server 12-digit signature.
        Assert.Equal("#Foo_____123", TableNameFormatter.StripGeneratedSuffix("#Foo_____123"));
    }

    [Fact]
    public void StripGeneratedSuffix_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TableNameFormatter.StripGeneratedSuffix(string.Empty));
    }

    [Fact]
    public void HasGeneratedSuffix_TempTableWithSqlServerPadding_True()
    {
        var name = "#Orders" + new string('_', 113) + "000000000157";
        Assert.True(TableNameFormatter.HasGeneratedSuffix(name));
    }

    [Fact]
    public void HasGeneratedSuffix_TempTableWithoutSuffix_False()
    {
        Assert.False(TableNameFormatter.HasGeneratedSuffix("#Foo"));
    }

    [Fact]
    public void HasGeneratedSuffix_GlobalTempTable_False()
    {
        Assert.False(TableNameFormatter.HasGeneratedSuffix("##GlobalTemp"));
    }

    [Fact]
    public void HasGeneratedSuffix_NonTempTable_False()
    {
        Assert.False(TableNameFormatter.HasGeneratedSuffix("Orders_____000000000157"));
    }

    [Fact]
    public void HasGeneratedSuffix_TrailingDigitsButNotTwelve_False()
    {
        Assert.False(TableNameFormatter.HasGeneratedSuffix("#Foo_____123"));
    }

    [Fact]
    public void HasGeneratedSuffix_Empty_False()
    {
        Assert.False(TableNameFormatter.HasGeneratedSuffix(string.Empty));
    }
}
