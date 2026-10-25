using Ardalis.Result;
using static GedcomSharp.Tests.TestData;

namespace GedcomSharp.Tests;

public class GedcomReaderTests
{
    [Fact]
    public void Parse_ReadsRecordsAndTypedViews()
    {
        var file = Parse(Gedcom551(
            "0 @I1@ INDI",
            "1 NAME John /Smith/",
            "1 SEX M",
            "1 BIRT",
            "2 DATE 1 JAN 1900",
            "2 PLAC London, England",
            "1 FAMS @F1@",
            "0 @I2@ INDI",
            "1 NAME Jane /Doe/",
            "1 SEX F",
            "1 FAMS @F1@",
            "0 @F1@ FAM",
            "1 HUSB @I1@",
            "1 WIFE @I2@"));

        Assert.Equal(GedcomVersion.V551, file.Version);
        var john = file.Find<IndividualRecord>("I1")!;
        Assert.Equal("John Smith", john.DisplayName);
        Assert.Equal("Smith", john.PrimaryName!.Surname);
        Assert.Equal(GedcomSex.Male, john.Sex);
        Assert.Equal(new DateOnly(1900, 1, 1), john.Birth!.Date.ToDateOnly());
        Assert.Equal("London, England", john.Birth.PlaceName);
        Assert.Equal("Jane Doe", Assert.Single(john.Spouses).DisplayName);
        Assert.Equal("F1", Assert.Single(file.Families).Xref);
    }

    [Fact]
    public void Parse_JoinsContinuationLinesAndUndoesEscapes()
    {
        var file = Parse(Gedcom551(
            "0 @N1@ NOTE First part, con",
            "1 CONC tinued",
            "1 CONT Second line with user@@example.com"));

        var note = Assert.Single(file.Notes);
        Assert.Equal("First part, continued\nSecond line with user@example.com", note.Text);
    }

    [Fact]
    public void Parse_Version7UndoesOnlyLeadingDoubledAt()
    {
        var file = Parse(Gedcom70(
            "0 @N1@ SNOTE @@handle and me@example.com"));

        Assert.Equal(GedcomVersion.V70, file.Version);
        Assert.Equal("@handle and me@example.com", Assert.Single(file.Notes).Text);
    }

    [Fact]
    public void Parse_EmptyTextIsInvalid()
    {
        var result = new GedcomReader().Parse(string.Empty);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Parse_FamilyTreeMakerTextExportIsInvalid()
    {
        var result = new GedcomReader().Parse(Lines("0 HEADER", "1 SOUR FTW"));
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Parse_ReportsUnresolvedPointers()
    {
        var file = Parse(Gedcom551(
            "0 @I1@ INDI",
            "1 FAMC @F9@"));

        Assert.Contains(file.Issues, issue => issue.Code == GedcomIssueCodes.UNRESOLVED_POINTER);
    }

    [Fact]
    public void Parse_StrictModeFailsOnErrors()
    {
        string text = Gedcom551("0 @I1@ INDI", "this line has no level or tag structure @", "1 NAME A /B/");
        var lenient = new GedcomReader().Parse(text);
        Assert.True(lenient.IsSuccess);

        var strict = new GedcomReader(new GedcomReaderOptions { Strict = true, RecoverLinesWithoutLevel = false }).Parse(text);
        Assert.Equal(ResultStatus.Invalid, strict.Status);
    }

    [Fact]
    public void Parse_RecoversWrappedNoteLines()
    {
        var file = Parse(Gedcom551(
            "0 @I1@ INDI",
            "1 NOTE A note that an old program",
            "wrapped onto a second line"));

        string? text = file.Find<IndividualRecord>("I1")!.Notes.Single().Text;
        Assert.Contains("wrapped onto a second line", text);
    }

    [Fact]
    public void Read_DecodesAnselCombiningMarks()
    {
        string header = Lines("0 HEAD", "1 GEDC", "2 VERS 5.5.1", "2 FORM LINEAGE-LINKED", "1 CHAR ANSEL", "0 @I1@ INDI", "1 NAME Ren");
        byte[] bytes = [.. Encoding.ASCII.GetBytes(header.TrimEnd('\r', '\n')), 0xE2, (byte)'e', .. Encoding.ASCII.GetBytes(" /Dupont/\r\n0 TRLR\r\n")];

        var result = new GedcomReader().Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.Equal(GedcomCharacterEncoding.Ansel, result.Value.Source!.Encoding);
        Assert.Equal("René Dupont", result.Value.Find<IndividualRecord>("I1")!.DisplayName);
    }

    [Fact]
    public void Read_DetectsCyrillicFileMislabelledAsAnsel()
    {
        var cyrillic = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;
        string text = Lines("0 HEAD", "1 GEDC", "2 VERS 5.5.1", "1 CHAR ANSEL", "0 @I1@ INDI", "1 NAME Иван /Овечкин/", "0 TRLR");

        var result = new GedcomReader().Read(cyrillic.GetBytes(text));

        Assert.True(result.IsSuccess);
        Assert.Equal("Иван Овечкин", result.Value.Find<IndividualRecord>("I1")!.DisplayName);
        Assert.Contains(result.Value.Issues, issue => issue.Code == GedcomIssueCodes.ENCODING_MISMATCH);
    }

    [Fact]
    public void Read_DetectsUtf16ByteOrderMark()
    {
        string text = Gedcom551("0 @I1@ INDI", "1 NAME Zoë /Ångström/");
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];

        var result = new GedcomReader().Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.Equal(GedcomCharacterEncoding.Utf16LittleEndian, result.Value.Source!.Encoding);
        Assert.Equal("Zoë Ångström", result.Value.Find<IndividualRecord>("I1")!.DisplayName);
    }

    [Fact]
    public void Read_KeepsUtf8CharactersWithoutByteOrderMark()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Gedcom551("0 @I1@ INDI", "1 NAME 李 /王/"));

        var result = new GedcomReader().Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.Equal("李 王", result.Value.Find<IndividualRecord>("I1")!.DisplayName);
    }

    [Theory]
    [InlineData("5.5", GedcomVersion.V551)]
    [InlineData("5.5.5", GedcomVersion.V555)]
    [InlineData("7.0", GedcomVersion.V70)]
    [InlineData("7.0.14", GedcomVersion.V70)]
    public void Parse_DetectsVersion(string declared, GedcomVersion expected)
    {
        // A 5.5 file in UTF-8 cannot be 5.5 (which had no UTF-8), so it is read as 5.5.1.
        var file = Parse(Lines("0 HEAD", "1 GEDC", $"2 VERS {declared}", "2 FORM LINEAGE-LINKED", "1 CHAR UTF-8", "0 TRLR"));
        Assert.Equal(expected, file.Version);
    }

    [Fact]
    public void Parse_KeepsUnknownStructures()
    {
        var file = Parse(Gedcom551(
            "0 @I1@ INDI",
            "1 _CUSTOM value",
            "2 _DEEPER nested"));

        var custom = file.Find("I1")!.Child("_CUSTOM")!;
        Assert.Equal("value", custom.Value);
        Assert.Equal("nested", custom.ChildValue("_DEEPER"));
    }
}
