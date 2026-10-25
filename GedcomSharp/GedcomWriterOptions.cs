using GedcomSharp.Gedzip;
using GedcomSharp.Text;

namespace GedcomSharp;

public sealed class GedcomWriterOptions
{
    public static GedcomWriterOptions Default { get; } = new();

    /// <summary>The version to write. Null writes the file's own version; any other version converts a copy first.</summary>
    public GedcomVersion? Version { get; set; }

    /// <summary>
    /// The character encoding. Null keeps the encoding the file was read in when writing the same version, so
    /// a rewrite does not lose characters the original could hold; otherwise UTF-8, except ANSEL for 5.5 (which predates UTF-8 support)
    /// unless the file was read from a code page such as Windows-1251 that ANSEL cannot hold.
    /// 7.0 allows only UTF-8 and 5.5.5 only UTF-8 or UTF-16; other choices fail the write.
    /// </summary>
    public GedcomCharacterEncoding? Encoding { get; set; }

    /// <summary>The line terminator. GEDCOM allows CR, LF or CR LF; CR LF is the most widely read.</summary>
    public string LineTerminator { get; set; } = "\r\n";

    /// <summary>Writes a byte order mark. Null writes one where the specification asks for it (5.5.5, 7.0) and for UTF-16.</summary>
    public bool? WriteByteOrderMark { get; set; }

    /// <summary>
    /// The longest line for 5.x output, counted in bytes of the target encoding (UTF-16: code units). Longer values
    /// are split with CONC. 5.x allows 255; 0 disables splitting. 7.0 has no limit and no CONC.
    /// </summary>
    public int MaxLineLength { get; set; } = 255;

    /// <summary>
    /// Doubles every @ in 5.x text (<c>user@@example.com</c>), as 5.x requires, so readers cannot confuse it with a
    /// pointer. Some older programs show the doubled sign; turn this off when targeting those.
    /// </summary>
    public bool EscapeAtSigns { get; set; } = true;

    /// <summary>Updates HEAD.GEDC, HEAD.CHAR and, when set, HEAD.SOUR and HEAD.DATE to match what is written.</summary>
    public bool UpdateHeader { get; set; } = true;

    /// <summary>Written to HEAD.SOUR (the exporting system's id) when set or when the header has none.</summary>
    public string? SourceSystem { get; set; }

    public string? SourceName { get; set; }

    public string? SourceVersion { get; set; }

    /// <summary>Written to HEAD.DATE/TIME when set.</summary>
    public DateTime? HeaderDate { get; set; }

    /// <summary>The dataset's entry name in a GEDZIP package.</summary>
    public string GedzipDatasetEntryName { get; set; } = GedzipPackage.DATASET_ENTRY_NAME;
}

/// <summary>What was written, with any issues found converting or encoding the data.</summary>
public sealed record GedcomWriteSummary(
    GedcomVersion Version,
    GedcomCharacterEncoding Encoding,
    int RecordCount,
    int LineCount,
    long ByteCount,
    IReadOnlyList<GedcomIssue> Issues);
