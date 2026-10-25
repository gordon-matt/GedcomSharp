using GedcomSharp.Text;

namespace GedcomSharp.Parsing;

/// <summary>
/// Decides which specification a file follows. The declared version is trusted except for "5.5", which most
/// exporters wrote for files that are really 5.5.1; those are refined with the algorithm from the GEDCOM 5.5.5
/// specification's bonus chapter (character set, HEAD.SOUR.CORP contents and known products).
/// </summary>
internal static class GedcomVersionDetector
{
    private static readonly (string Product, Version? MinimumVersion)[] products551 =
    [
        ("PAF", new Version(5, 0)),
        ("ANCESTQUEST", new Version(12, 0)),
        ("FTM", new Version(21, 0, 0, 466)),
        ("FTW", new Version(21, 0, 0, 466)),
        ("GENOPRO", new Version(2, 0)),
        ("GRAMPS", new Version(2, 0)),
        ("LIFELINES", new Version(3, 0)),
        ("MACFAMILYTREE", new Version(5, 7, 8)),
        ("MYHERITAGE", new Version(5, 5)),
        ("REUNION", new Version(9, 0)),
        ("ROOTSMAGIC", null),
        ("PRO-GEN", new Version(3, 0)),
        ("MAGIKEY", null)
    ];

    public static GedcomVersion Detect(GedcomStructure? header, IReadOnlyList<GedcomStructure> records, GedcomCharacterEncoding encoding, GedcomIssueCollector issues)
    {
        string? declared = header?.Find(GedcomTags.GEDC, GedcomTags.VERS)?.Value?.Trim();
        var version = GedcomVersionExtensions.ParseVersion(declared);

        if (version == GedcomVersion.Unknown)
        {
            bool looksSeven = header?.Child(GedcomTags.SCHMA) is not null
                || records.Any(record => record.Tag == GedcomTags.SNOTE);
            version = looksSeven ? GedcomVersion.V70 : GedcomVersion.V551;
            issues.Warning(
                GedcomIssueCodes.VERSION,
                string.IsNullOrEmpty(declared)
                    ? $"The header does not declare a GEDCOM version; it was read as {version.ToVersionString()}."
                    : $"GEDCOM version '{declared}' is not recognised; it was read as {version.ToVersionString()}.");
            return version;
        }

        if (version == GedcomVersion.V55 && declared is not ("5.5" or "5.5.0" or "55"))
        {
            issues.Info(GedcomIssueCodes.VERSION, $"GEDCOM version '{declared}' was read as 5.5.");
        }

        if (version == GedcomVersion.V55 && header is not null && LooksLike551(header, encoding, out string reason))
        {
            issues.Info(GedcomIssueCodes.VERSION, $"The file declares GEDCOM 5.5 but {reason}; it was read as 5.5.1.");
            return GedcomVersion.V551;
        }

        return version;
    }

    private static bool LooksLike551(GedcomStructure header, GedcomCharacterEncoding encoding, out string reason)
    {
        string? charSet = header.ChildValue(GedcomTags.CHAR)?.Trim().ToUpperInvariant();
        if (encoding == GedcomCharacterEncoding.Utf8 && charSet is "UTF-8" or "UTF8")
        {
            reason = "it is UTF-8, which 5.5 does not allow";
            return true;
        }

        var source = header.Child(GedcomTags.SOUR);
        var corporation = source?.Child(GedcomTags.CORP);
        if (corporation is not null)
        {
            IEnumerable<GedcomStructure> addressParts = corporation.Child(GedcomTags.ADDR)?.Children ?? Enumerable.Empty<GedcomStructure>();
            if (corporation.Child(GedcomTags.EMAIL, GedcomTags.FAX, GedcomTags.WWW) is not null
                || addressParts.Any(part => part.Tag == GedcomTags.ADR3))
            {
                reason = "HEAD.SOUR.CORP uses 5.5.1 address tags";
                return true;
            }

            if (corporation.Child(GedcomTags.Extensions.EMAIL, GedcomTags.Extensions.FAX, GedcomTags.Extensions.WWW) is not null)
            {
                reason = string.Empty;
                return false;
            }
        }

        if (source?.Value is string product)
        {
            string productVersionText = source.ChildValue(GedcomTags.VERS) ?? string.Empty;
            string normalizedProduct = product.Trim().ToUpperInvariant();

            // Family Tree Maker writes its build in the product name: "Family Tree Maker (21.0.0.466)".
            if (normalizedProduct.StartsWith("FAMILY TREE MAKER", StringComparison.Ordinal) || normalizedProduct == "FTM")
            {
                normalizedProduct = "FTM";
                int open = product.IndexOf('(');
                int close = product.IndexOf(')', open + 1);
                if (open >= 0 && close > open)
                {
                    productVersionText = product[(open + 1)..close];
                }
            }

            foreach (var (name, minimum) in products551)
            {
                if (!normalizedProduct.StartsWith(name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (minimum is null || (TryParseProductVersion(productVersionText, out var actual) && actual >= minimum))
                {
                    reason = $"it was exported by {product.Trim()} {productVersionText}".TrimEnd();
                    return true;
                }
            }
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryParseProductVersion(string text, out Version version)
    {
        var builder = new StringBuilder();
        foreach (char c in text.Trim())
        {
            if (char.IsAsciiDigit(c) || c == '.')
            {
                builder.Append(c);
            }
            else if (builder.Length > 0)
            {
                break;
            }
        }

        string digits = builder.ToString().Trim('.');
        if (!digits.Contains('.'))
        {
            digits += ".0";
        }

        return Version.TryParse(digits, out version!);
    }
}
