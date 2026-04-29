using StatisticsParser.Core.Models;
using StatisticsParser.Core.Parsing;
using Xunit;

namespace StatisticsParser.Core.Tests;

public class ParserLanguageTests
{
    public static TheoryData<string> AllLanguageCodes => new()
    {
        "en",
        "es",
        "it",
    };

    [Theory]
    [MemberData(nameof(AllLanguageCodes))]
    public void AllLanguageSingletons_LoadAndPopulateColumnVariants(string langValue)
    {
        var lang = langValue switch
        {
            "en" => ParserLanguage.English,
            "es" => ParserLanguage.Spanish,
            "it" => ParserLanguage.Italian,
            _ => null
        };

        Assert.NotNull(lang);
        Assert.Equal(langValue, lang!.LangValue);
        Assert.Contains(lang, ParserLanguage.All);

        Assert.False(string.IsNullOrEmpty(lang.LangName));
        Assert.False(string.IsNullOrEmpty(lang.Table));
        Assert.False(string.IsNullOrEmpty(lang.ExecutionTime));
        Assert.False(string.IsNullOrEmpty(lang.CompileTime));
        Assert.False(string.IsNullOrEmpty(lang.CpuTime));
        Assert.False(string.IsNullOrEmpty(lang.ElapsedTime));
        Assert.False(string.IsNullOrEmpty(lang.Milliseconds));
        Assert.False(string.IsNullOrEmpty(lang.ErrorMsg));
        Assert.False(string.IsNullOrEmpty(lang.CompletionTimeLabel));

        Assert.NotEmpty(lang.RowsAffected);
        Assert.NotEmpty(lang.Scan);
        Assert.NotEmpty(lang.Logical);
        Assert.NotEmpty(lang.Physical);
        Assert.NotEmpty(lang.PageServer);
        Assert.NotEmpty(lang.ReadAhead);
        Assert.NotEmpty(lang.PageServerReadAhead);
        Assert.NotEmpty(lang.LobLogical);
        Assert.NotEmpty(lang.LobPhysical);
        Assert.NotEmpty(lang.LobPageServer);
        Assert.NotEmpty(lang.LobReadAhead);
        Assert.NotEmpty(lang.LobPageServerReadAhead);
        Assert.NotEmpty(lang.SegmentReads);
        Assert.NotEmpty(lang.SegmentSkipped);
    }

    [Fact]
    public void All_ContainsThreeLanguagesInOrder()
    {
        Assert.Equal(3, ParserLanguage.All.Count);
        Assert.Same(ParserLanguage.English, ParserLanguage.All[0]);
        Assert.Same(ParserLanguage.Spanish, ParserLanguage.All[1]);
        Assert.Same(ParserLanguage.Italian, ParserLanguage.All[2]);
    }

    [Theory]
    [InlineData("en", "scan count", IoColumn.Scan)]
    [InlineData("en", "Scan Count", IoColumn.Scan)]
    [InlineData("en", "SCAN COUNT", IoColumn.Scan)]
    [InlineData("en", "  scan count  ", IoColumn.Scan)]
    [InlineData("en", "logical reads", IoColumn.Logical)]
    [InlineData("en", "Read-Ahead Reads", IoColumn.ReadAhead)]
    [InlineData("en", "bogus column", IoColumn.NotFound)]
    [InlineData("es", "Recuento de Exámenes", IoColumn.Scan)]
    [InlineData("es", "lecturas lógicas", IoColumn.Logical)]
    [InlineData("es", "lecturas lógicas de LOB", IoColumn.LobLogical)]
    [InlineData("es", "Lecturas Anticipadas", IoColumn.ReadAhead)]
    [InlineData("it", "Conteggio analisi", IoColumn.Scan)]
    [InlineData("it", "letture logiche", IoColumn.Logical)]
    [InlineData("it", "letture logiche LOB", IoColumn.LobLogical)]
    [InlineData("it", "Letture Read-Ahead", IoColumn.ReadAhead)]
    public void DetermineIoColumn_IsCaseInsensitiveAndTrims(string langValue, string input, IoColumn expected)
    {
        var lang = langValue switch
        {
            "en" => ParserLanguage.English,
            "es" => ParserLanguage.Spanish,
            "it" => ParserLanguage.Italian,
            _ => null
        };

        Assert.NotNull(lang);
        Assert.Equal(expected, lang!.DetermineIoColumn(input));
    }
}
