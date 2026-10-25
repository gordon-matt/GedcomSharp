namespace GedcomSharp.Tests;

/// <summary>
/// Reads and rewrites every .ged file in the folders listed in the GEDCOMSHARP_CORPUS environment variable
/// (separated by ';'). Real-world files are not checked in, so the test does nothing when the variable is unset.
/// </summary>
public class CorpusTests
{
    public static TheoryData<string> CorpusFiles()
    {
        var data = new TheoryData<string>();
        string? folders = Environment.GetEnvironmentVariable("GEDCOMSHARP_CORPUS");
        var files = (folders ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.ged", SearchOption.AllDirectories))
            .ToList();

        if (files.Count == 0)
        {
            data.Add(string.Empty);
        }

        foreach (string file in files)
        {
            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public async Task ReadWriteRead_KeepsEveryRecord(string path)
    {
        if (path.Length == 0)
        {
            return;
        }

        var reader = new GedcomReader();
        var first = await reader.ReadAsync(path);
        if (!first.IsSuccess)
        {
            // Deliberately broken samples (empty files, FTW text exports) are rejected, which is the expected outcome.
            return;
        }

        byte[] written = new GedcomWriter(new GedcomWriterOptions { UpdateHeader = false }).WriteToBytes(first.Value).Value;
        var second = reader.Read(written, path);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Records.Count, second.Value.Records.Count);
        for (int i = 0; i < first.Value.Records.Count; i++)
        {
            Assert.True(
                first.Value.Records[i].ContentEquals(second.Value.Records[i]),
                $"{Path.GetFileName(path)}: record {first.Value.Records[i].Xref ?? first.Value.Records[i].Tag} changed on round-trip.");
        }
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public async Task ConvertToEveryVersion_WritesReadableOutput(string path)
    {
        if (path.Length == 0)
        {
            return;
        }

        var reader = new GedcomReader();
        var original = await reader.ReadAsync(path);
        if (!original.IsSuccess)
        {
            return;
        }

        foreach (var version in (GedcomVersion[])[GedcomVersion.V55, GedcomVersion.V551, GedcomVersion.V555, GedcomVersion.V70])
        {
            var written = new GedcomWriter(new GedcomWriterOptions { Version = version }).WriteToBytes(original.Value);
            Assert.True(written.IsSuccess, $"{Path.GetFileName(path)} could not be written as {version}.");

            var read = reader.Read(written.Value, path);
            Assert.True(read.IsSuccess, $"{Path.GetFileName(path)} written as {version} could not be read back.");
            // The reader refines "5.5" to 5.5.1 for UTF-8 files and for exporters known to mislabel 5.5.1 as 5.5,
            // and HEAD.SOUR still names the original exporter after conversion.
            bool expected = read.Value.Version == version || (version == GedcomVersion.V55 && read.Value.Version == GedcomVersion.V551);
            Assert.True(
                expected,
                $"{Path.GetFileName(path)} written as {version} was read as {read.Value.Version}: {string.Join("; ", read.Value.Issues.Where(issue => issue.Code == GedcomIssueCodes.VERSION).Select(issue => issue.Message))}");
            Assert.DoesNotContain(read.Value.Issues, issue => issue.Severity == GedcomIssueSeverity.Error);
        }
    }
}
