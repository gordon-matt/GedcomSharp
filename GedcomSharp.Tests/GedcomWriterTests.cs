using Ardalis.Result;
using static GedcomSharp.Tests.TestData;

namespace GedcomSharp.Tests;

public class GedcomWriterTests
{
    [Fact]
    public void WriteToString_SplitsLongLegacyLinesWithConc()
    {
        var file = GedcomFile.Create(GedcomVersion.V551);
        string text = string.Join(' ', Enumerable.Repeat("genealogy", 80));
        file.AddNote(text, "N1");

        string output = Write(file);

        Assert.Contains("1 CONC ", output);
        Assert.All(output.Split("\r\n"), line => Assert.True(line.Length <= 255, $"Line too long: {line.Length}"));
        Assert.Equal(text, Assert.Single(Parse(output).Notes).Text);
    }

    [Fact]
    public void WriteToString_Version7HasNoConc()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        file.AddNote(new string('x', 1000), "N1");

        string output = Write(file);

        Assert.DoesNotContain("CONC", output);
        Assert.Contains("0 @N1@ SNOTE ", output);
    }

    [Fact]
    public void WriteToString_WritesNewlinesAsCont()
    {
        var file = GedcomFile.Create(GedcomVersion.V551);
        file.AddNote("line one\nline two", "N1");

        string output = Write(file);

        Assert.Contains("0 @N1@ NOTE line one\r\n1 CONT line two\r\n", output);
    }

    [Fact]
    public void WriteToString_EscapesAtSignsPerVersion()
    {
        var legacy = GedcomFile.Create(GedcomVersion.V551);
        legacy.AddNote("@me and you@example.com", "N1");
        Assert.Contains("NOTE @@me and you@@example.com", Write(legacy));

        var seven = GedcomFile.Create(GedcomVersion.V70);
        seven.AddNote("@me and you@example.com", "N1");
        Assert.Contains("SNOTE @@me and you@example.com", Write(seven));
    }

    [Fact]
    public void WriteToString_UpdatesHeaderAndEndsWithTrailer()
    {
        var file = GedcomFile.Create(GedcomVersion.V555);
        file.AddIndividual("I1").AddName("Ann", "Lee");

        string output = Write(file, new GedcomWriterOptions { SourceSystem = "MyApp" });

        Assert.StartsWith("0 HEAD\r\n", output);
        Assert.Contains("1 SOUR MyApp", output);
        Assert.Contains("2 VERS 5.5.5", output);
        Assert.Contains("0 @U1@ SUBM", output);
        Assert.EndsWith("0 TRLR\r\n", output);
    }

    [Fact]
    public void WriteToBytes_Version7IsUtf8WithByteOrderMark()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        file.AddIndividual("I1").AddName("Zoë", "Ångström");

        byte[] bytes = new GedcomWriter().WriteToBytes(file).Value;

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Contains("Zoë", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void WriteToBytes_AnselRoundTrips()
    {
        var file = GedcomFile.Create(GedcomVersion.V551);
        file.AddIndividual("I1").AddName("René", "Müller");

        byte[] bytes = new GedcomWriter(new GedcomWriterOptions { Encoding = GedcomCharacterEncoding.Ansel }).WriteToBytes(file).Value;
        var read = new GedcomReader().Read(bytes).Value;

        Assert.Equal(GedcomCharacterEncoding.Ansel, read.Source!.Encoding);
        Assert.Equal("René Müller", read.Find<IndividualRecord>("I1")!.DisplayName);
    }

    [Fact]
    public void WriteToString_RejectsEncodingTheVersionForbids()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        var result = new GedcomWriter(new GedcomWriterOptions { Encoding = GedcomCharacterEncoding.Ansel }).WriteToString(file);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Write_RoundTripsWithoutLosingStructures()
    {
        string original = Gedcom551(
            "0 @I1@ INDI",
            "1 NAME John /Smith/",
            "2 GIVN John",
            "2 SURN Smith",
            "1 BIRT",
            "2 DATE ABT 1850",
            "2 _CUSTOM kept",
            "0 @S1@ SOUR",
            "1 TITL Parish register",
            "0 @N1@ NOTE A shared note");
        var first = Parse(original);

        var second = Parse(Write(first, new GedcomWriterOptions { UpdateHeader = false }));

        Assert.Equal(first.Records.Count, second.Records.Count);
        for (int i = 0; i < first.Records.Count; i++)
        {
            Assert.True(first.Records[i].ContentEquals(second.Records[i]), $"Record {first.Records[i].Xref} changed.");
        }
    }

    [Fact]
    public void WriteToString_ConvertsWhenTargetVersionDiffers()
    {
        var file = GedcomFile.Create(GedcomVersion.V551);
        file.AddNote("Shared", "N1");

        string output = Write(file, new GedcomWriterOptions { Version = GedcomVersion.V70 });

        Assert.Contains("2 VERS 7.0", output);
        Assert.Contains("0 @N1@ SNOTE Shared", output);
        Assert.Equal(GedcomVersion.V551, file.Version);
    }
}
