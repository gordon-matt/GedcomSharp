namespace GedcomSharp.Model;

/// <summary>
/// <c>NAME</c>: a name in <c>Given /Surname/ Suffix</c> form with optional parts (GIVN, SURN, ...).
/// The parts, when present, are more reliable than splitting the value, so the getters prefer them.
/// </summary>
public sealed class PersonalName(GedcomStructure structure) : GedcomCitableView(structure)
{
    /// <summary>The full value, e.g. <c>John /Smith/ Jr.</c>.</summary>
    public string? FullValue
    {
        get => Value;
        set => Value = value;
    }

    /// <summary>The name with the surname slashes removed, for display.</summary>
    public string DisplayName => string.Join(' ', (Value ?? string.Empty).Replace("/", " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public string? Given
    {
        get => Get(GedcomTags.GIVN) ?? Split(Value).Given;
        set => Set(GedcomTags.GIVN, value);
    }

    public string? Surname
    {
        get => Get(GedcomTags.SURN) ?? Split(Value).Surname;
        set => Set(GedcomTags.SURN, value);
    }

    public string? Prefix
    {
        get => Get(GedcomTags.NPFX);
        set => Set(GedcomTags.NPFX, value);
    }

    public string? Suffix
    {
        get => Get(GedcomTags.NSFX) ?? Split(Value).Suffix;
        set => Set(GedcomTags.NSFX, value);
    }

    public string? SurnamePrefix
    {
        get => Get(GedcomTags.SPFX);
        set => Set(GedcomTags.SPFX, value);
    }

    public string? Nickname
    {
        get => Get(GedcomTags.NICK);
        set => Set(GedcomTags.NICK, value);
    }

    /// <summary>The married name extension most programs use (<c>_MARNM</c>).</summary>
    public string? MarriedName
    {
        get => Get(GedcomTags.Extensions.MARNM);
        set => Set(GedcomTags.Extensions.MARNM, value);
    }

    /// <summary><c>TYPE</c>: BIRTH, MAIDEN, MARRIED, AKA, IMMIGRANT, PROFESSIONAL, OTHER (lowercase words in 5.5.1).</summary>
    public string? Type
    {
        get => Get(GedcomTags.TYPE);
        set => Set(GedcomTags.TYPE, value);
    }

    /// <summary>Variants of the name in other scripts or languages: 7.0 TRAN, 5.5.1 FONE/ROMN.</summary>
    public IEnumerable<PersonalName> Translations => Views([GedcomTags.TRAN, GedcomTags.FONE, GedcomTags.ROMN], child => new PersonalName(child));

    /// <summary>Builds a NAME value: <c>Given /Surname/ Suffix</c>.</summary>
    public static string FormatValue(string? given, string? surname, string? suffix = null)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(given))
        {
            builder.Append(given.Trim());
        }

        if (surname is not null)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append('/').Append(surname.Trim()).Append('/');
        }

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            builder.Append(' ').Append(suffix.Trim());
        }

        return builder.ToString();
    }

    /// <summary>Splits a NAME value on its slashes. Text before the surname is the given name; text after it is the suffix.</summary>
    public static (string? Given, string? Surname, string? Suffix) Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null, null);
        }

        int open = value.IndexOf('/');
        if (open < 0)
        {
            return (Clean(value), null, null);
        }

        int close = value.IndexOf('/', open + 1);
        if (close < 0)
        {
            return (Clean(value[..open]), Clean(value[(open + 1)..]), null);
        }

        return (Clean(value[..open]), Clean(value[(open + 1)..close]), Clean(value[(close + 1)..]));
    }

    private static string? Clean(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
