namespace GedcomSharp;

/// <summary>
/// The enumerated values of GEDCOM 7.0 and how loose 5.x values map onto them. 5.5.1 wrote most of these in
/// lowercase and allowed free text for some (NAME.TYPE, ASSO.RELA); 7.0 uses uppercase values plus OTHER with a PHRASE.
/// </summary>
public static class GedcomEnumerations
{
    public static readonly IReadOnlySet<string> Roles = new HashSet<string>(StringComparer.Ordinal)
    {
        "CHIL", "CLERGY", "FATH", "FRIEND", "GODP", "HUSB", "MOTH", "MULTIPLE", "NGHBR", "OFFICIATOR", "PARENT", "SPOU", "WIFE", "WITN", "OTHER"
    };

    public static readonly IReadOnlySet<string> NameTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "AKA", "BIRTH", "IMMIGRANT", "MAIDEN", "MARRIED", "PROFESSIONAL", "OTHER"
    };

    public static readonly IReadOnlySet<string> Pedigrees = new HashSet<string>(StringComparer.Ordinal)
    {
        "ADOPTED", "BIRTH", "FOSTER", "SEALING", "OTHER"
    };

    public static readonly IReadOnlySet<string> Media = new HashSet<string>(StringComparer.Ordinal)
    {
        "AUDIO", "BOOK", "CARD", "ELECTRONIC", "FICHE", "FILM", "MAGAZINE", "MANUSCRIPT", "MAP", "NEWSPAPER", "PHOTO", "TOMBSTONE", "VIDEO", "OTHER"
    };

    public static readonly IReadOnlySet<string> Restrictions = new HashSet<string>(StringComparer.Ordinal)
    {
        "CONFIDENTIAL", "LOCKED", "PRIVACY"
    };

    public static readonly IReadOnlySet<string> ChildLinkStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "CHALLENGED", "DISPROVEN", "PROVEN"
    };

    private static readonly Dictionary<string, string> roleSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CHILD"] = "CHIL",
        ["SON"] = "CHIL",
        ["DAUGHTER"] = "CHIL",
        ["CLERGY"] = "CLERGY",
        ["PRIEST"] = "CLERGY",
        ["MINISTER"] = "CLERGY",
        ["PASTOR"] = "CLERGY",
        ["RABBI"] = "CLERGY",
        ["FATHER"] = "FATH",
        ["FRIEND"] = "FRIEND",
        ["GODPARENT"] = "GODP",
        ["GODFATHER"] = "GODP",
        ["GODMOTHER"] = "GODP",
        ["SPONSOR"] = "GODP",
        ["HUSBAND"] = "HUSB",
        ["MOTHER"] = "MOTH",
        ["NEIGHBOR"] = "NGHBR",
        ["NEIGHBOUR"] = "NGHBR",
        ["OFFICIANT"] = "OFFICIATOR",
        ["OFFICIATOR"] = "OFFICIATOR",
        ["PARENT"] = "PARENT",
        ["SPOUSE"] = "SPOU",
        ["WIFE"] = "WIFE",
        ["WITNESS"] = "WITN"
    };

    private static readonly Dictionary<string, string> nameTypeSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALSO KNOWN AS"] = "AKA",
        ["ALIAS"] = "AKA",
        ["NICKNAME"] = "AKA",
        ["BIRTH NAME"] = "BIRTH",
        ["MAIDEN NAME"] = "MAIDEN",
        ["MARRIED NAME"] = "MARRIED"
    };

    /// <summary>Maps an ASSO RELA/ROLE text to a 7.0 role; <paramref name="exact"/> is false when OTHER is returned for unknown text.</summary>
    public static string ToRole(string value, out bool exact) => ToEnumeration(value, Roles, roleSynonyms, out exact);

    public static string ToNameType(string value, out bool exact) => ToEnumeration(value, NameTypes, nameTypeSynonyms, out exact);

    public static string ToPedigree(string value, out bool exact) => ToEnumeration(value, Pedigrees, null, out exact);

    public static string ToMedium(string value, out bool exact) => ToEnumeration(value, Media, null, out exact);

    /// <summary>The 5.5.1 RELA text for a 7.0 role.</summary>
    public static string ToRelation(string role) => role switch
    {
        "CHIL" => "Child",
        "CLERGY" => "Clergy",
        "FATH" => "Father",
        "FRIEND" => "Friend",
        "GODP" => "Godparent",
        "HUSB" => "Husband",
        "MOTH" => "Mother",
        "MULTIPLE" => "Multiple",
        "NGHBR" => "Neighbor",
        "OFFICIATOR" => "Officiator",
        "PARENT" => "Parent",
        "SPOU" => "Spouse",
        "WIFE" => "Wife",
        "WITN" => "Witness",
        _ => role
    };

    private static string ToEnumeration(string value, IReadOnlySet<string> allowed, Dictionary<string, string>? synonyms, out bool exact)
    {
        string upper = value.Trim().ToUpperInvariant();
        if (allowed.Contains(upper))
        {
            exact = true;
            return upper;
        }

        if (synonyms is not null && synonyms.TryGetValue(upper, out string? mapped))
        {
            exact = true;
            return mapped;
        }

        exact = false;
        return "OTHER";
    }
}
