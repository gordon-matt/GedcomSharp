using GedcomSharp.Gedzip;

namespace GedcomSharp;

/// <summary>Writes a <see cref="GedcomFile"/> as GEDCOM 5.5, 5.5.1, 5.5.5 or 7.0, or as a GEDZIP package.</summary>
public interface IGedcomWriter
{
    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is the outcome for the gedcom write summary.</returns>
    Task<Result<GedcomWriteSummary>> WriteAsync(GedcomFile file, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the value.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is the outcome for the gedcom write summary.</returns>
    Task<Result<GedcomWriteSummary>> WriteAsync(GedcomFile file, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>The encoded file bytes, including any byte order mark.</summary>
    Result<byte[]> WriteToBytes(GedcomFile file);

    /// <summary>The GEDCOM text, before encoding.</summary>
    Result<string> WriteToString(GedcomFile file);

    /// <summary>Writes a GEDZIP package: the dataset plus the given media files.</summary>
    Task<Result<GedcomWriteSummary>> WriteGedzipAsync(GedcomFile file, Stream stream, IEnumerable<GedzipFileEntry>? mediaFiles = null, CancellationToken cancellationToken = default);
}
