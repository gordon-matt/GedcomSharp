namespace GedcomSharp.Conversion;

/// <summary>Maps the GEDCOM 5.5.1 language names (<c>English</c>, <c>Serbo_Croa</c>) to the BCP 47 tags 7.0 uses, and back.</summary>
public static class GedcomLanguages
{
    private static readonly (string Name, string Tag)[] languages =
    [
        ("Afrikaans", "af"), ("Albanian", "sq"), ("Amharic", "am"), ("Anglo-Saxon", "ang"), ("Arabic", "ar"),
        ("Armenian", "hy"), ("Assamese", "as"), ("Belorusian", "be"), ("Bengali", "bn"), ("Braj", "bra"),
        ("Bulgarian", "bg"), ("Burmese", "my"), ("Cantonese", "yue"), ("Catalan", "ca"), ("Catalan_Spn", "ca-ES"),
        ("Church-Slavic", "cu"), ("Czech", "cs"), ("Danish", "da"), ("Dogri", "doi"), ("Dutch", "nl"),
        ("English", "en"), ("Esperanto", "eo"), ("Estonian", "et"), ("Faroese", "fo"), ("Finnish", "fi"),
        ("French", "fr"), ("Georgian", "ka"), ("German", "de"), ("Greek", "el"), ("Gujarati", "gu"),
        ("Hawaiian", "haw"), ("Hebrew", "he"), ("Hindi", "hi"), ("Hungarian", "hu"), ("Icelandic", "is"),
        ("Indonesian", "id"), ("Italian", "it"), ("Japanese", "ja"), ("Kannada", "kn"), ("Khmer", "km"),
        ("Konkani", "kok"), ("Korean", "ko"), ("Lahnda", "lah"), ("Lao", "lo"), ("Latvian", "lv"),
        ("Lithuanian", "lt"), ("Macedonian", "mk"), ("Maithili", "mai"), ("Malayalam", "ml"), ("Mandrin", "cmn"),
        ("Manipuri", "mni"), ("Marathi", "mr"), ("Mewari", "mtr"), ("Navaho", "nv"), ("Nepali", "ne"),
        ("Norwegian", "no"), ("Oriya", "or"), ("Pahari", "him"), ("Pali", "pi"), ("Panjabi", "pa"),
        ("Persian", "fa"), ("Polish", "pl"), ("Portuguese", "pt"), ("Prakrit", "pra"), ("Pusto", "ps"),
        ("Rajasthani", "raj"), ("Romanian", "ro"), ("Russian", "ru"), ("Sanskrit", "sa"), ("Serb", "sr"),
        ("Serbo_Croa", "sh"), ("Slovak", "sk"), ("Slovene", "sl"), ("Spanish", "es"), ("Swedish", "sv"),
        ("Tagalog", "tl"), ("Tamil", "ta"), ("Telugu", "te"), ("Thai", "th"), ("Tibetan", "bo"),
        ("Turkish", "tr"), ("Ukrainian", "uk"), ("Urdu", "ur"), ("Vietnamese", "vi"), ("Wendic", "wen"),
        ("Yiddish", "yi")
    ];

    private static readonly Dictionary<string, string> tagsByName = languages.ToDictionary(language => language.Name, language => language.Tag, StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> namesByTag = languages
        .GroupBy(language => language.Tag, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>The BCP 47 tag for a 5.x language name; values that already look like tags are returned unchanged. Null when unknown.</summary>
    public static string? ToTag(string value)
    {
        string trimmed = value.Trim();
        if (tagsByName.TryGetValue(trimmed, out string? tag))
        {
            return tag;
        }

        bool looksLikeTag = (trimmed.Length is >= 2 and <= 3 && trimmed.All(char.IsAsciiLetter))
            || (trimmed.Contains('-') && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
        if (looksLikeTag)
        {
            return trimmed;
        }

        var culture = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(info => string.Equals(info.EnglishName, trimmed, StringComparison.OrdinalIgnoreCase));
        return culture is { Name.Length: > 0 } ? culture.Name : null;
    }

    /// <summary>The 5.x language name for a BCP 47 tag (by its primary subtag), or the tag itself when there is none.</summary>
    public static string ToName(string tag)
    {
        string trimmed = tag.Trim();
        if (namesByTag.TryGetValue(trimmed, out string? name))
        {
            return name;
        }

        int dash = trimmed.IndexOf('-');
        if (dash > 0 && namesByTag.TryGetValue(trimmed[..dash], out string? primary))
        {
            return primary;
        }

        return trimmed;
    }
}
