using GedcomSharp.Gedzip;

namespace GedcomSharp;

/// <summary>Writes a <see cref="GedcomFile"/> as GEDCOM 5.5, 5.5.1, 5.5.5 or 7.0, or as a GEDZIP package.</summary>
public interface IGedcomWriter
{
    Task<Result<GedcomWriteSummary>> WriteAsync(GedcomFile file, string path, CancellationToken cancellationToken = default);

    Task<Result<GedcomWriteSummary>> WriteAsync(GedcomFile file, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>The encoded file bytes, including any byte order mark.</summary>
    Result<byte[]> WriteToBytes(GedcomFile file);

    /// <summary>The GEDCOM text, before encoding.</summary>
    Result<string> WriteToString(GedcomFile file);

    /// <summary>Writes a GEDZIP package: the dataset plus the given media files.</summary>
    Task<Result<GedcomWriteSummary>> WriteGedzipAsync(GedcomFile file, Stream stream, IEnumerable<GedzipFileEntry>? mediaFiles = null, CancellationToken cancellationToken = default);
}
