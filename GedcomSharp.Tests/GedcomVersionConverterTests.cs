using static GedcomSharp.Tests.TestData;

namespace GedcomSharp.Tests;

public class GedcomVersionConverterTests
{
    [Fact]
    public void ConvertTo7_RestructuresLegacyData()
    {
        var file = Parse(Gedcom551(
            "0 @I1@ INDI",
            "1 NAME John /Smith/",
            "2 TYPE birth",
            "1 BIRT",
            "2 DATE INT 1850 (from census)",
            "1 DEAT",
            "2 AGE CHILD",
            "1 NOTE @N1@",
            "1 FAMC @F1@",
            "2 PEDI adopted",
            "1 EMAI john@example.com",
            "0 @F1@ FAM",
            "1 CHIL @I1@",
            "0 @N1@ NOTE Shared note",
            "0 @O1@ OBJE",
            "1 FILE C:\\Photos\\john smith.jpg",
            "2 FORM jpg"));

        var converted = file.ConvertTo(GedcomVersion.V70);

        Assert.Equal(GedcomVersion.V70, converted.Version);
        Assert.Equal(GedcomVersion.V551, file.Version);
        var person = converted.Find("I1")!;
        Assert.Equal("SNOTE", person.Child("SNOTE")?.Tag);
        Assert.Equal("SNOTE", converted.Find("N1")!.Tag);
        Assert.Equal("BIRTH", person.Find("NAME", "TYPE")!.Value);
        Assert.Equal("1850", person.Find("BIRT", "DATE")!.Value);
        Assert.Equal("from census", person.Find("BIRT", "DATE", "PHRASE")!.Value);
        Assert.Equal("< 8y", person.Find("DEAT", "AGE")!.Value);
        Assert.Equal("ADOPTED", person.Find("FAMC", "PEDI")!.Value);
        Assert.Equal("john@example.com", person.ChildValue("EMAIL"));

        var media = converted.Find("O1")!.Child("FILE")!;
        Assert.Equal("file:///C:/Photos/john%20smith.jpg", media.Value);
        Assert.Equal("image/jpeg", media.ChildValue("FORM"));
        Assert.DoesNotContain(converted.HeaderStructure.Children, child => child.Tag == "CHAR");
    }

    [Fact]
    public void ConvertTo551_RestoresLegacyForms()
    {
        var file = Parse(Gedcom70(
            "0 @I1@ INDI",
            "1 NAME Jane /Doe/",
            "1 BIRT",
            "2 DATE 1850",
            "3 PHRASE from census",
            "1 SNOTE @N1@",
            "1 SEX X",
            "0 @N1@ SNOTE Shared note"));

        var converted = file.ConvertTo(GedcomVersion.V551);
        var person = converted.Find("I1")!;

        Assert.Equal("NOTE", converted.Find("N1")!.Tag);
        Assert.Equal("N1", person.Child("NOTE")!.Pointer);
        Assert.Equal("INT 1850 (from census)", person.Find("BIRT", "DATE")!.Value);
        Assert.Equal("U", person.ChildValue("SEX"));
        Assert.Equal("5.5.1", converted.HeaderStructure.Find("GEDC", "VERS")!.Value);
    }

    [Fact]
    public void ConvertTo555_AddsSubmitterAndShortensXrefs()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        file.AddIndividual("PERSON_WITH_A_VERY_LONG_ID");

        var converted = file.ConvertTo(GedcomVersion.V555);

        Assert.Single(converted.Submitters);
        Assert.All(converted.Records, record => Assert.True(record.Xref!.Length <= 20 && record.Xref.All(char.IsAsciiLetterOrDigit)));
    }
}
