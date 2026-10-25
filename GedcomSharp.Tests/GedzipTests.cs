using GedcomSharp.Gedzip;

namespace GedcomSharp.Tests;

public class GedzipTests
{
    [Fact]
    public async Task WriteAndRead_KeepsDatasetAndMedia()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        file.AddIndividual("I1").AddName("Ann", "Lee");
        file.AddMultimedia("media/photo 1.jpg", "O1");
        byte[] photo = [1, 2, 3, 4];

        using var stream = new MemoryStream();
        var written = await new GedcomWriter().WriteGedzipAsync(file, stream, [GedzipFileEntry.FromBytes("media/photo 1.jpg", photo)]);
        Assert.True(written.IsSuccess);

        stream.Position = 0;
        var read = await new GedcomReader().ReadGedzipAsync(stream);

        Assert.True(read.IsSuccess);
        using var package = read.Value;
        Assert.Equal(GedzipPackage.DATASET_ENTRY_NAME, package.DatasetEntryName);
        Assert.Equal("Ann Lee", package.File.Find<IndividualRecord>("I1")!.DisplayName);
        Assert.Empty(package.MissingFiles);
        Assert.Equal(photo, await package.ReadFileAsync("media/photo%201.jpg"));
    }

    [Fact]
    public async Task Read_ReportsMissingMedia()
    {
        var file = GedcomFile.Create(GedcomVersion.V70);
        file.AddMultimedia("media/missing.jpg", "O1");

        using var stream = new MemoryStream();
        await new GedcomWriter().WriteGedzipAsync(file, stream);
        stream.Position = 0;
        using var package = (await new GedcomReader().ReadGedzipAsync(stream)).Value;

        Assert.Equal("media/missing.jpg", Assert.Single(package.MissingFiles));
    }
}
