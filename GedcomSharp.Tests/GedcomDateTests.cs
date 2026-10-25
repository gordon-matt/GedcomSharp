namespace GedcomSharp.Tests;

public class GedcomDateTests
{
    [Theory]
    [InlineData("1 JAN 1900", GedcomDateKind.Simple)]
    [InlineData("ABT 1900", GedcomDateKind.About)]
    [InlineData("CAL 1900", GedcomDateKind.Calculated)]
    [InlineData("EST 1900", GedcomDateKind.Estimated)]
    [InlineData("BEF 1900", GedcomDateKind.Before)]
    [InlineData("AFT 1900", GedcomDateKind.After)]
    [InlineData("BET 1900 AND 1910", GedcomDateKind.Between)]
    [InlineData("FROM 1900 TO 1910", GedcomDateKind.FromTo)]
    [InlineData("FROM 1900", GedcomDateKind.From)]
    [InlineData("TO 1910", GedcomDateKind.To)]
    [InlineData("INT 1900 (about then)", GedcomDateKind.Interpreted)]
    [InlineData("(sometime in spring)", GedcomDateKind.Phrase)]
    public void Parse_RecognisesStandardForms(string text, GedcomDateKind kind)
    {
        var date = GedcomDate.Parse(text);
        Assert.Equal(kind, date.Kind);
        Assert.False(date.WasNormalized);
    }

    [Theory]
    [InlineData("circa 1900", GedcomDateKind.About)]
    [InlineData("1900?", GedcomDateKind.About)]
    [InlineData("1900-05-17", GedcomDateKind.Simple)]
    [InlineData("17.05.1900", GedcomDateKind.Simple)]
    [InlineData("May 17, 1900", GedcomDateKind.Simple)]
    [InlineData("1900-1910", GedcomDateKind.Between)]
    public void Parse_NormalizesCommonNonStandardForms(string text, GedcomDateKind kind)
    {
        var date = GedcomDate.Parse(text);
        Assert.Equal(kind, date.Kind);
        Assert.True(date.WasNormalized);
    }

    [Fact]
    public void Parse_ReadsCalendarEscapesAndKeywords()
    {
        var legacy = GedcomDate.Parse("@#DJULIAN@ 1 JAN 1700");
        var seven = GedcomDate.Parse("JULIAN 1 JAN 1700");

        Assert.Equal(GedcomCalendar.Julian, legacy.Start!.Calendar);
        Assert.Equal(legacy, seven);
        Assert.Equal("@#DJULIAN@ 1 JAN 1700", legacy.Format(GedcomVersion.V551));
        Assert.Equal("JULIAN 1 JAN 1700", legacy.Format(GedcomVersion.V70));
    }

    [Fact]
    public void Parse_ReadsDualYears()
    {
        var date = GedcomDate.Parse("11 FEB 1699/00");

        Assert.Equal(1699, date.Start!.Year);
        Assert.Equal(1700, date.Start.DualYear);
        Assert.Equal("11 FEB 1699/00", date.Format(GedcomVersion.V551));
    }

    [Fact]
    public void Format_WritesBceForEachVersion()
    {
        var date = GedcomDate.Parse("44 B.C.");

        Assert.True(date.Start!.IsBce);
        Assert.Equal("44 B.C.", date.Format(GedcomVersion.V551));
        Assert.Equal("44 BCE", date.Format(GedcomVersion.V70));
    }

    [Fact]
    public void ToDateOnly_ConvertsHebrewDates()
    {
        // Rosh Hashanah 5785 fell on 3 October 2024.
        var date = GedcomDate.Parse("@#DHEBREW@ 1 TSH 5785");
        Assert.Equal(new DateOnly(2024, 10, 3), date.ToDateOnly());
    }

    [Fact]
    public void ToDateOnly_ConvertsFrenchRepublicanDates()
    {
        // The Republican calendar began on 22 September 1792.
        var date = GedcomDate.Parse("@#DFRENCH R@ 1 VEND 1");
        Assert.Equal(new DateOnly(1792, 9, 22), date.ToDateOnly());
    }

    [Fact]
    public void ToDateOnly_ConvertsJulianDates()
    {
        // 5 October 1582 Julian was the day after the last Julian day in Catholic countries: 15 October Gregorian.
        var date = GedcomDate.Parse("@#DJULIAN@ 5 OCT 1582");
        Assert.Equal(new DateOnly(1582, 10, 15), date.ToDateOnly());
    }

    [Fact]
    public void CompareTo_OrdersByMeaning()
    {
        string[] texts = ["1910", "BEF 1900", "1 JAN 1900", "ABT 1905"];
        var sorted = texts.Select(GedcomDate.Parse).Order().Select(date => date.OriginalText).ToList();
        Assert.Equal(["BEF 1900", "1 JAN 1900", "ABT 1905", "1910"], sorted);
    }

    [Theory]
    [InlineData("CHILD", "< 8y", "CHILD")]
    [InlineData("> 35y 6m", "> 35y 6m", "> 35y 6m")]
    [InlineData("2w", "2w", "14d")]
    public void Age_FormatsPerVersion(string text, string seven, string legacy)
    {
        var age = GedcomAge.Parse(text)!;
        Assert.Equal(seven, age.Format(GedcomVersion.V70));
        Assert.Equal(legacy, age.Format(GedcomVersion.V551));
    }

    [Theory]
    [InlineData("35 years", 35)]
    [InlineData("35", 35)]
    public void Age_ParsesLooseForms(string text, int years) => Assert.Equal(years, GedcomAge.Parse(text)!.Years);
}
