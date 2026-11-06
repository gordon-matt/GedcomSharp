using System.Text.RegularExpressions;

namespace GedcomSharp.Values;

/// <summary>
/// Specifies the gedcom age bound.
/// </summary>
public enum GedcomAgeBound
{
    /// <summary>
    /// The exact value.
    /// </summary>
    Exact,
    /// <summary>
    /// The less than value.
    /// </summary>
    LessThan,
    /// <summary>
    /// The greater than value.
    /// </summary>
    GreaterThan
}

/// <summary>The 5.x age keywords. 7.0 replaced them with durations (<c>&lt; 8y</c>, <c>&lt; 1y</c>, <c>0y</c>) plus a PHRASE.</summary>
public enum GedcomAgeKeyword
{
    /// <summary>
    /// The child value.
    /// </summary>
    Child,
    /// <summary>
    /// The infant value.
    /// </summary>
    Infant,
    /// <summary>
    /// The stillborn value.
    /// </summary>
    Stillborn
}

/// <summary>An age at an event (<c>AGE</c>): an optional bound and a duration in years, months, weeks and days.</summary>
public sealed partial record GedcomAge
{
    /// <summary>
    /// Gets or sets the bound.
    /// </summary>
    public GedcomAgeBound Bound { get; init; }

    /// <summary>
    /// Gets or sets the years.
    /// </summary>
    public int? Years { get; init; }

    /// <summary>
    /// Gets or sets the months.
    /// </summary>
    public int? Months { get; init; }

    /// <summary>Weeks were added in 7.0; 5.x output converts them to days.</summary>
    public int? Weeks { get; init; }

    /// <summary>
    /// Gets or sets the days.
    /// </summary>
    public int? Days { get; init; }

    /// <summary>
    /// Gets or sets the keyword.
    /// </summary>
    public GedcomAgeKeyword? Keyword { get; init; }

    /// <summary>
    /// Gets or sets the original text.
    /// </summary>
    public string? OriginalText { get; init; }

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    public bool IsEmpty => Keyword is null && Years is null && Months is null && Weeks is null && Days is null;

    /// <summary>
    /// Creates a value from the years.
    /// </summary>
    /// <param name="years">The years.</param>
    /// <param name="bound">The bound.</param>
    /// <returns>The years.</returns>
    public static GedcomAge FromYears(int years, GedcomAgeBound bound = GedcomAgeBound.Exact) => new() { Years = years, Bound = bound };

    /// <summary>Parses 5.x and 7.0 ages and loose forms like <c>35</c>, <c>35 years</c> or <c>2 yrs 3 mos</c>. Returns null when nothing is recognised.</summary>
    public static GedcomAge? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string value = text.Trim().ToUpperInvariant();
        var keyword = value switch
        {
            "CHILD" => GedcomAgeKeyword.Child,
            "INFANT" => GedcomAgeKeyword.Infant,
            "STILLBORN" => GedcomAgeKeyword.Stillborn,
            _ => (GedcomAgeKeyword?)null
        };

        if (keyword is not null)
        {
            return new GedcomAge { Keyword = keyword, OriginalText = text };
        }

        var bound = GedcomAgeBound.Exact;
        if (value.StartsWith('<'))
        {
            bound = GedcomAgeBound.LessThan;
            value = value[1..].TrimStart();
        }
        else if (value.StartsWith('>'))
        {
            bound = GedcomAgeBound.GreaterThan;
            value = value[1..].TrimStart();
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int bare))
        {
            return new GedcomAge { Bound = bound, Years = bare, OriginalText = text };
        }

        var matches = PartRegex().Matches(value);
        if (matches.Count == 0 || PartRegex().Replace(value, string.Empty).Trim(' ', ',').Length > 0)
        {
            return null;
        }

        var age = new GedcomAge { Bound = bound, OriginalText = text };
        foreach (Match match in matches)
        {
            int number = int.Parse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            age = match.Groups["unit"].Value[0] switch
            {
                'Y' or 'J' or 'A' => age with { Years = (age.Years ?? 0) + number },
                'M' => age with { Months = (age.Months ?? 0) + number },
                'W' => age with { Weeks = (age.Weeks ?? 0) + number },
                _ => age with { Days = (age.Days ?? 0) + number }
            };
        }

        return age;
    }

    /// <summary>Writes the age for <paramref name="version"/>. Keywords become durations in 7.0, the meaning kept by the caller as a PHRASE.</summary>
    public string Format(GedcomVersion version)
    {
        if (Keyword is GedcomAgeKeyword keyword)
        {
            if (!version.IsSeven)
            {
                return keyword.ToString().ToUpperInvariant();
            }

            return keyword switch
            {
                GedcomAgeKeyword.Child => "< 8y",
                GedcomAgeKeyword.Infant => "< 1y",
                _ => "0y"
            };
        }

        var parts = new List<string>(4);
        if (Years is int years)
        {
            parts.Add($"{years}y");
        }

        if (Months is int months)
        {
            parts.Add($"{months}m");
        }

        int? days = Days;
        if (Weeks is int weeks)
        {
            if (version.IsSeven)
            {
                parts.Add($"{weeks}w");
            }
            else
            {
                days = (days ?? 0) + (weeks * 7);
            }
        }

        if (days is int d)
        {
            parts.Add($"{d}d");
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        string duration = string.Join(' ', parts);
        return Bound switch
        {
            GedcomAgeBound.LessThan => "< " + duration,
            GedcomAgeBound.GreaterThan => "> " + duration,
            _ => duration
        };
    }

    /// <inheritdoc/>
    public override string ToString() => Format(GedcomVersion.V70);

    [GeneratedRegex(@"(?<number>\d+)\s*(?<unit>Y(?:EARS?|RS?|EAR)?|J(?:AHRE?)?|A(?:NS?|NOS?)?|M(?:ONTHS?|OS?|TH)?|W(?:EEKS?|KS?)?|D(?:AYS?)?)\.?(?![A-Z])")]
    private static partial Regex PartRegex();
}
