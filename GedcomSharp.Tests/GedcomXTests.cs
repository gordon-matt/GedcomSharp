using GedcomSharp.GedcomX;

namespace GedcomSharp.Tests;

public class GedcomXTests
{
    private readonly GedcomXSerializer serializer = new();

    private static GedcomFile CreateFamily()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        var father = file.AddIndividual("I1");
        father.AddName("John", "Smith");
        father.Sex = GedcomSex.Male;
        father.AddEvent(GedcomTags.BIRT, GedcomDate.Parse("ABT 1850"), "London, England");
        var mother = file.AddIndividual("I2");
        mother.AddName("Mary", "Jones");
        mother.Sex = GedcomSex.Female;
        var child = file.AddIndividual("I3");
        child.AddName("Tom", "Smith");
        file.AddFamily(father, mother, child);
        return file;
    }

    [Fact]
    public void ToGedcomX_MapsPeopleAndRelationships()
    {
        var document = GedcomXConverter.ToGedcomX(CreateFamily());

        Assert.Equal(3, document.Persons!.Count);
        var john = document.FindPerson("I1")!;
        Assert.Equal(GedcomXTypes.Gender.MALE, john.Gender!.Type);
        var birth = Assert.Single(john.Facts!);
        Assert.Equal("http://gedcomx.org/Birth", birth.Type);
        Assert.Equal("A+1850", birth.Date!.Formal);
        Assert.Single(document.Relationships!, relationship => relationship.Type == GedcomXTypes.Relationship.COUPLE);
        Assert.Equal(2, document.Relationships!.Count(relationship => relationship.Type == GedcomXTypes.Relationship.PARENT_CHILD));
    }

    [Fact]
    public void FromGedcomX_RebuildsFamilies()
    {
        var file = GedcomXConverter.FromGedcomX(GedcomXConverter.ToGedcomX(CreateFamily()));

        var family = Assert.Single(file.Families);
        Assert.Equal("John Smith", family.Husband!.DisplayName);
        Assert.Equal("Mary Jones", family.Wife!.DisplayName);
        Assert.Equal("Tom Smith", Assert.Single(family.Children).DisplayName);
        Assert.Equal(GedcomDateKind.About, family.Husband.Birth!.Date.Kind);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var document = GedcomXConverter.ToGedcomX(CreateFamily());

        string json = serializer.ToJson(document);
        var read = serializer.FromJson(json);

        Assert.True(read.IsSuccess);
        Assert.Contains("\"persons\"", json);
        Assert.Equal(json, serializer.ToJson(read.Value));
    }

    [Fact]
    public void Xml_RoundTrips()
    {
        var document = GedcomXConverter.ToGedcomX(CreateFamily());

        string xml = serializer.ToXml(document);
        var read = serializer.FromXml(xml);

        Assert.True(read.IsSuccess);
        Assert.Contains("http://gedcomx.org/v1/", xml);
        Assert.Equal(serializer.ToJson(document), serializer.ToJson(read.Value));
    }

    [Fact]
    public async Task GedxFile_RoundTrips()
    {
        var file = new GedcomXFile();
        file.Documents["tree.json"] = GedcomXConverter.ToGedcomX(CreateFamily());
        file.Resources["images/photo.jpg"] = [1, 2, 3];

        using var stream = new MemoryStream();
        Assert.True((await serializer.WriteFileAsync(file, stream)).IsSuccess);
        stream.Position = 0;
        var read = await serializer.ReadFileAsync(stream);

        Assert.True(read.IsSuccess);
        Assert.Equal(3, read.Value.MainDocument!.Persons!.Count);
        Assert.Equal([1, 2, 3], read.Value.Resources["images/photo.jpg"]);
        Assert.True(read.Value.Manifest.ContainsKey("tree.json"));
    }

    [Theory]
    [InlineData("+1850")]
    [InlineData("A+1850-03")]
    [InlineData("+1850-03-12T10:30:00Z")]
    [InlineData("+1850/+1860")]
    [InlineData("A/+1860")]
    [InlineData("-0044")]
    public void Date_ParsesAndFormatsFormalDates(string formal)
    {
        Assert.True(GedcomXDate.TryParse(formal, out var date));
        Assert.Equal(formal, date.ToString());
    }
}
