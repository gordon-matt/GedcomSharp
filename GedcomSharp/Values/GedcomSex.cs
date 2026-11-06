namespace GedcomSharp.Values;

/// <summary>
/// Specifies the gedcom sex.
/// </summary>
public enum GedcomSex
{
    /// <summary>No SEX structure.</summary>
    NotRecorded,

    /// <summary>
    /// The male value.
    /// </summary>
    Male,
    /// <summary>
    /// The female value.
    /// </summary>
    Female,

    /// <summary><c>X</c>: does not fit the male/female categories. 7.0 and 5.5.5 only; 5.5 and 5.5.1 output writes <c>U</c>.</summary>
    Intersex,

    /// <summary><c>U</c>: cannot be determined from available sources.</summary>
    Unknown
}

/// <summary>
/// Provides extension methods for gedcom sexs.
/// </summary>
public static class GedcomSexExtensions
{
    extension(GedcomSex sex)
    {
        /// <summary>
        /// Converts the value to the code.
        /// </summary>
        /// <returns>The code.</returns>
        public string? ToCode() => sex switch
        {
            GedcomSex.Male => "M",
            GedcomSex.Female => "F",
            GedcomSex.Intersex => "X",
            GedcomSex.Unknown => "U",
            _ => null
        };
    }

    /// <summary>Reads a SEX value, accepting words (<c>male</c>, <c>female</c>) as well as codes.</summary>
    public static GedcomSex ParseSex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GedcomSex.NotRecorded;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "M" or "MALE" or "MAN" or "H" => GedcomSex.Male,
            "F" or "FEMALE" or "WOMAN" or "W" => GedcomSex.Female,
            "X" or "INTERSEX" or "OTHER" => GedcomSex.Intersex,
            _ => GedcomSex.Unknown
        };
    }
}
