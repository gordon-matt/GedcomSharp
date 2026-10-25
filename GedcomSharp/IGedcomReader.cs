using GedcomSharp.Gedzip;

namespace GedcomSharp;

/// <summary>Reads GEDCOM 5.5, 5.5.1, 5.5.5 and 7.0 datasets and GEDZIP packages.</summary>
public interface IGedcomReader
{
    /// <summary>Reads a .ged file, or the dataset inside a .gdz/.zip GEDZIP package.</summary>
    Task<Result<GedcomFile>> ReadAsync(string path, CancellationToken cancellationToken = default);

    Task<Result<GedcomFile>> ReadAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>Reads raw file bytes, detecting the character encoding.</summary>
    Result<GedcomFile> Read(byte[] data, string? fileName = null);

    /// <summary>Parses GEDCOM text that has already been decoded.</summary>
    Result<GedcomFile> Parse(string text);

    /// <summary>Opens a GEDZIP package, keeping the archive open so its media files can be read.</summary>
    Task<Result<GedzipPackage>> ReadGedzipAsync(string path, CancellationToken cancellationToken = default);

    Task<Result<GedzipPackage>> ReadGedzipAsync(Stream stream, CancellationToken cancellationToken = default);
}
