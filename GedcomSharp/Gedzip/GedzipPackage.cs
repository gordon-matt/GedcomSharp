using System.IO.Compression;

namespace GedcomSharp.Gedzip;

/// <summary>
/// An open GEDZIP package (<c>.gdz</c>): a ZIP archive holding a GEDCOM dataset and the media files it references.
/// GEDZIP 1.0 (GEDCOM 7) names the dataset <c>gedcom.ged</c>; the earlier 0.x drafts allowed any single <c>.ged</c>.
/// Dispose the package to close the archive.
/// </summary>
public sealed class GedzipPackage : IDisposable
{
    public const string DATASET_ENTRY_NAME = "gedcom.ged";

    private readonly ZipArchive archive;
    private readonly Dictionary<string, ZipArchiveEntry> entries;

    internal GedzipPackage(ZipArchive zipArchive, GedcomFile file, string datasetEntryName)
    {
        archive = zipArchive;
        File = file;
        DatasetEntryName = datasetEntryName;
        entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in zipArchive.Entries)
        {
            if (entry.FullName.Length > 0 && !entry.FullName.EndsWith('/'))
            {
                entries.TryAdd(entry.FullName.Replace('\\', '/'), entry);
            }
        }
    }

    public GedcomFile File { get; }

    public string DatasetEntryName { get; }

    /// <summary>The names of all files in the archive except the dataset.</summary>
    public IReadOnlyList<string> EntryNames => entries.Keys.Where(name => name != DatasetEntryName).ToList();

    /// <summary>
    /// Multimedia FILE references that are local paths (not web URLs) but are not in the archive. A package
    /// is only complete when this is empty.
    /// </summary>
    public IReadOnlyList<string> MissingFiles => File.Multimedia
        .SelectMany(record => record.Files)
        .Concat(File.Records.SelectMany(record => record.Descendants())
            .Where(structure => structure.Tag == GedcomTags.OBJE && !structure.HasPointer && structure.Parent is not null)
            .SelectMany(structure => structure.ChildrenWithTag(GedcomTags.FILE))
            .Select(structure => new MultimediaFile(structure)))
        .Select(file => file.Path)
        .OfType<string>()
        .Where(path => IsLocalReference(path) && ResolveEntryName(path) is null)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    public bool Contains(string fileReference) => ResolveEntryName(fileReference) is not null;

    /// <summary>
    /// Finds the archive entry for a FILE value. GEDCOM 7 references are relative URLs (<c>media/photo%201.jpg</c>);
    /// older packages often kept absolute Windows paths, so those are matched by their longest tail that exists in the archive.
    /// </summary>
    public string? ResolveEntryName(string fileReference)
    {
        if (string.IsNullOrWhiteSpace(fileReference))
        {
            return null;
        }

        string path = Normalize(fileReference);
        if (entries.ContainsKey(path))
        {
            return path;
        }

        string? caseInsensitive = entries.Keys.FirstOrDefault(name => string.Equals(name, path, StringComparison.OrdinalIgnoreCase));
        if (caseInsensitive is not null)
        {
            return caseInsensitive;
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int skip = 1; skip < segments.Length; skip++)
        {
            string tail = string.Join('/', segments[skip..]);
            string? match = entries.Keys.FirstOrDefault(name => string.Equals(name, tail, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Opens a referenced file for reading, or returns null when it is not in the archive.</summary>
    public Stream? OpenFile(string fileReference) =>
        ResolveEntryName(fileReference) is string name ? entries[name].Open() : null;

    public async Task<byte[]?> ReadFileAsync(string fileReference, CancellationToken cancellationToken = default)
    {
        await using var stream = OpenFile(fileReference);
        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    public void Dispose() => archive.Dispose();

    internal static bool IsZip(byte[] data) => data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] is 0x03 or 0x05 or 0x07;

    internal static ZipArchiveEntry? FindDatasetEntry(ZipArchive archive)
    {
        var root = archive.Entries.FirstOrDefault(entry => string.Equals(entry.FullName, DATASET_ENTRY_NAME, StringComparison.OrdinalIgnoreCase));
        if (root is not null)
        {
            return root;
        }

        var datasets = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".ged", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName.Count(c => c == '/'))
            .ToList();
        return datasets.FirstOrDefault();
    }

    /// <summary>Turns a FILE value into an archive path: percent-decoded, forward slashes, no scheme, drive or leading slash.</summary>
    internal static string Normalize(string reference)
    {
        string value = reference.Trim();
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[5..].TrimStart('/');
        }

        value = Uri.UnescapeDataString(value).Replace('\\', '/');
        if (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':')
        {
            value = value[2..];
        }

        while (value.StartsWith("./", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return value.TrimStart('/');
    }

    private static bool IsLocalReference(string path)
    {
        int colon = path.IndexOf(':');
        if (colon <= 1)
        {
            return true;
        }

        return path.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A media file to store in a GEDZIP package under <paramref name="EntryName"/> (the FILE value, as a relative path).</summary>
public sealed record GedzipFileEntry(string EntryName, Func<CancellationToken, Task<Stream>> OpenAsync)
{
    public static GedzipFileEntry FromPath(string entryName, string filePath) =>
        new(entryName, _ => Task.FromResult<Stream>(System.IO.File.OpenRead(filePath)));

    public static GedzipFileEntry FromBytes(string entryName, byte[] data) =>
        new(entryName, _ => Task.FromResult<Stream>(new MemoryStream(data, writable: false)));
}
