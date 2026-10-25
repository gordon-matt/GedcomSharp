namespace GedcomSharp.Tests;

internal static class TestData
{
    public static string Lines(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    public static string Gedcom551(params string[] records) => Lines(
    [
        "0 HEAD",
        "1 SOUR TEST",
        "1 GEDC",
        "2 VERS 5.5.1",
        "2 FORM LINEAGE-LINKED",
        "1 CHAR UTF-8",
        .. records,
        "0 TRLR"
    ]);

    public static string Gedcom70(params string[] records) => Lines(
    [
        "0 HEAD",
        "1 GEDC",
        "2 VERS 7.0",
        .. records,
        "0 TRLR"
    ]);

    public static GedcomFile Parse(string text, GedcomReaderOptions? options = null)
    {
        var result = new GedcomReader(options).Parse(text);
        Assert.True(result.IsSuccess, string.Join("; ", result.ValidationErrors.Select(error => error.ErrorMessage).Concat(result.Errors)));
        return result.Value;
    }

    public static string Write(GedcomFile file, GedcomWriterOptions? options = null)
    {
        var result = new GedcomWriter(options).WriteToString(file);
        Assert.True(result.IsSuccess, string.Join("; ", result.ValidationErrors.Select(error => error.ErrorMessage).Concat(result.Errors)));
        return result.Value;
    }
}
