namespace GedcomSharp;

/// <summary>
/// The GEDCOM specification a dataset follows. 5.5, 5.5.1 and 5.5.5 share one grammar with different rules
/// for encodings, escapes and multimedia; 7.0 changed the grammar (no CONC, only a leading @ is doubled,
/// calendar keywords instead of escapes), so most behaviour branches on <c>IsLegacy</c>.
/// </summary>
public enum GedcomVersion
{
    Unknown = 0,

    /// <summary>GEDCOM 5.5 (1996). Also used for files that claim 5.3, 5.4 or earlier.</summary>
    V55 = 550,

    /// <summary>GEDCOM 5.5.1 (1999), the most widely supported version.</summary>
    V551 = 551,

    /// <summary>GEDCOM 5.5.5 (2019), a strict Unicode-only maintenance release of 5.5.1.</summary>
    V555 = 555,

    /// <summary>FamilySearch GEDCOM 7.0 and its minor revisions (7.0.x).</summary>
    V70 = 700
}

public static class GedcomVersionExtensions
{
    extension(GedcomVersion version)
    {
        /// <summary>True for the 5.x family (5.5, 5.5.1, 5.5.5), which shares the CONC/escape grammar.</summary>
        public bool IsLegacy => version is GedcomVersion.V55 or GedcomVersion.V551 or GedcomVersion.V555;

        public bool IsSeven => version == GedcomVersion.V70;

        /// <summary>The value written to <c>HEAD.GEDC.VERS</c>.</summary>
        public string ToVersionString() => version switch
        {
            GedcomVersion.V55 => "5.5",
            GedcomVersion.V551 => "5.5.1",
            GedcomVersion.V555 => "5.5.5",
            GedcomVersion.V70 => "7.0",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Maps a <c>HEAD.GEDC.VERS</c> value to a version. 5.5 is returned as-is; the reader refines it to 5.5.1
    /// using the header heuristics from the 5.5.5 specification, because most "5.5" files are really 5.5.1.
    /// </summary>
    public static GedcomVersion ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GedcomVersion.Unknown;
        }

        string text = value.Trim();
        if (text.StartsWith('7') || text.StartsWith('8') || text.StartsWith('9'))
        {
            return GedcomVersion.V70;
        }

        return text switch
        {
            "5.5.5" => GedcomVersion.V555,
            "5.5.1" or "5.5.1.0" => GedcomVersion.V551,
            "5.5" or "5.5.0" or "55" => GedcomVersion.V55,
            _ when text.StartsWith("5.5.1", StringComparison.Ordinal) => GedcomVersion.V551,
            _ when text.StartsWith("5.5.5", StringComparison.Ordinal) => GedcomVersion.V555,
            _ when text.StartsWith("5.", StringComparison.Ordinal) || text.StartsWith("4.", StringComparison.Ordinal) => GedcomVersion.V55,
            _ => GedcomVersion.Unknown
        };
    }
}
