namespace GedcomSharp.Values;

public enum GedcomDateKind
{
    Empty,

    /// <summary>A plain date: <c>1 JAN 1900</c>, <c>JAN 1900</c> or <c>1900</c>.</summary>
    Simple,

    About,
    Calculated,
    Estimated,
    Before,
    After,

    /// <summary><c>BET x AND y</c>: the event happened on one day in the range.</summary>
    Between,

    /// <summary><c>FROM x</c>: a state that started on that date.</summary>
    From,

    /// <summary><c>TO y</c>: a state that ended on that date. The date is in <see cref="GedcomDate.End"/>.</summary>
    To,

    /// <summary><c>FROM x TO y</c>: a state that lasted for the whole period.</summary>
    FromTo,

    /// <summary>5.x <c>INT 1900 (phrase)</c>, or a 7.0 date with a PHRASE: a date interpreted from the phrase.</summary>
    Interpreted,

    /// <summary>Only a phrase: 5.x <c>(phrase)</c>, or a 7.0 empty DATE with a PHRASE.</summary>
    Phrase,

    /// <summary>Text that is not a valid date. It is kept as written in <see cref="GedcomDate.OriginalText"/>.</summary>
    Unparsed
}

/// <summary>
/// A GEDCOM date value: a kind (exact, approximate, range, period), up to two calendar dates and an optional phrase.
/// <see cref="Parse(string?)"/> accepts the 5.x and 7.0 syntaxes plus common non-standard forms (<c>circa 1900</c>,
/// <c>January 5, 1900</c>, <c>1900-01-05</c>, <c>5.1.1900</c>, <c>1900?</c>) so that dates from any exporter can be
/// compared, sorted and written back in standard form.
/// </summary>
public sealed record GedcomDate : IComparable<GedcomDate>
{
    public static GedcomDate Empty { get; } = new() { Kind = GedcomDateKind.Empty };

    public GedcomDateKind Kind { get; init; }

    /// <summary>The date for single-date kinds, or the start of a range or period. Null for <see cref="GedcomDateKind.To"/>.</summary>
    public GedcomDatePart? Start { get; init; }

    /// <summary>The end of <see cref="GedcomDateKind.Between"/>, <see cref="GedcomDateKind.FromTo"/> and the date of <see cref="GedcomDateKind.To"/>.</summary>
    public GedcomDatePart? End { get; init; }

    /// <summary>Free text about the date: the 5.x INT/() phrase or the 7.0 PHRASE substructure.</summary>
    public string? Phrase { get; init; }

    /// <summary>The value as read, when the date was parsed from text.</summary>
    public string? OriginalText { get; init; }

    /// <summary>True when the text was not in standard GEDCOM syntax but was understood (e.g. <c>circa 1900</c>).</summary>
    public bool WasNormalized { get; init; }

    public bool IsEmpty => Kind == GedcomDateKind.Empty;

    /// <summary>True when the value has at least one calendar date.</summary>
    public bool HasDate => Start is not null || End is not null;

    public bool IsRange => Kind is GedcomDateKind.Between or GedcomDateKind.FromTo;

    /// <summary>The first date in the value (Start, or End for TO dates).</summary>
    public GedcomDatePart? Date => Start ?? End;

    public bool IsApproximate => Kind is GedcomDateKind.About or GedcomDateKind.Calculated or GedcomDateKind.Estimated;

    public static GedcomDate Exact(int year, int? month = null, int? day = null, GedcomCalendar calendar = GedcomCalendar.Gregorian) =>
        new() { Kind = GedcomDateKind.Simple, Start = new GedcomDatePart(year, month, day, calendar) };

    public static GedcomDate FromDateOnly(DateOnly date) => new() { Kind = GedcomDateKind.Simple, Start = GedcomDatePart.FromDateOnly(date) };

    public static GedcomDate Create(GedcomDateKind kind, GedcomDatePart date) => kind switch
    {
        GedcomDateKind.To => new GedcomDate { Kind = kind, End = date },
        GedcomDateKind.Simple or GedcomDateKind.About or GedcomDateKind.Calculated or GedcomDateKind.Estimated
            or GedcomDateKind.Before or GedcomDateKind.After or GedcomDateKind.From => new GedcomDate { Kind = kind, Start = date },
        _ => throw new ArgumentException($"{kind} needs two dates or a phrase.", nameof(kind))
    };

    public static GedcomDate Between(GedcomDatePart start, GedcomDatePart end) => new() { Kind = GedcomDateKind.Between, Start = start, End = end };

    public static GedcomDate Period(GedcomDatePart? start, GedcomDatePart? end) => (start, end) switch
    {
        (not null, not null) => new GedcomDate { Kind = GedcomDateKind.FromTo, Start = start, End = end },
        (not null, null) => new GedcomDate { Kind = GedcomDateKind.From, Start = start },
        (null, not null) => new GedcomDate { Kind = GedcomDateKind.To, End = end },
        _ => Empty
    };

    public static GedcomDate FromPhrase(string phrase) => new() { Kind = GedcomDateKind.Phrase, Phrase = phrase };

    /// <summary>Parses a date leniently. Never throws; text that is not a date gives <see cref="GedcomDateKind.Unparsed"/>.</summary>
    public static GedcomDate Parse(string? text) => GedcomDateParser.Parse(text);

    /// <summary>Parses a date and its 7.0 PHRASE substructure.</summary>
    public static GedcomDate Parse(string? text, string? phrase)
    {
        var date = Parse(text);
        if (string.IsNullOrEmpty(phrase))
        {
            return date;
        }

        return date.Kind switch
        {
            GedcomDateKind.Empty => FromPhrase(phrase),
            GedcomDateKind.Simple => date with { Kind = GedcomDateKind.Interpreted, Phrase = phrase },
            _ => date with { Phrase = phrase }
        };
    }

    public static bool TryParse(string? text, out GedcomDate date)
    {
        date = Parse(text);
        return date.Kind is not (GedcomDateKind.Unparsed or GedcomDateKind.Empty);
    }

    /// <summary>The earliest day the value allows, as a Julian Day Number; null when open-ended or not computable.</summary>
    public int? MinJulianDay => Kind switch
    {
        GedcomDateKind.Before => null,
        GedcomDateKind.To => null,
        GedcomDateKind.After => Start?.MaxJulianDay + 1,
        _ => Start?.MinJulianDay
    };

    /// <summary>The latest day the value allows, as a Julian Day Number; null when open-ended or not computable.</summary>
    public int? MaxJulianDay => Kind switch
    {
        GedcomDateKind.Before => Start?.MinJulianDay - 1,
        GedcomDateKind.After or GedcomDateKind.From => null,
        GedcomDateKind.Between or GedcomDateKind.FromTo or GedcomDateKind.To => End?.MaxJulianDay,
        _ => Start?.MaxJulianDay
    };

    /// <summary>A single Julian Day for sorting; dates without one sort last.</summary>
    public int? SortJulianDay => Kind switch
    {
        GedcomDateKind.Before => Start?.MinJulianDay - 1,
        GedcomDateKind.After => Start?.MaxJulianDay + 1,
        GedcomDateKind.To => End?.MinJulianDay,
        _ => Start?.MinJulianDay
    };

    /// <summary>The year of the first date, for display and quick filtering.</summary>
    public int? Year => Date?.Year is int year ? (Date.IsBce ? -year : year) : null;

    public DateOnly? ToDateOnly() => Kind is GedcomDateKind.Simple or GedcomDateKind.Interpreted ? Start?.ToDateOnly() : null;

    public int CompareTo(GedcomDate? other)
    {
        if (other is null)
        {
            return -1;
        }

        return (SortJulianDay, other.SortJulianDay) switch
        {
            (int left, int right) => left.CompareTo(right),
            (null, null) => 0,
            (null, _) => 1,
            _ => -1
        };
    }

    /// <summary>Writes the value in the syntax of <paramref name="version"/>; in 7.0 the phrase must be written separately (see <see cref="FormatWithPhrase"/>).</summary>
    public string Format(GedcomVersion version) => FormatWithPhrase(version).Value;

    /// <summary>
    /// The DATE payload and, for 7.0, the PHRASE to write with it. 7.0 has no INT, phrase-only dates or dual years,
    /// so those become a date plus a PHRASE; 5.x folds a phrase into <c>INT date (phrase)</c> where it can.
    /// </summary>
    public (string Value, string? Phrase) FormatWithPhrase(GedcomVersion version)
    {
        bool seven = version.IsSeven;
        switch (Kind)
        {
            case GedcomDateKind.Empty:
                return (string.Empty, null);
            case GedcomDateKind.Unparsed:
                return seven ? (string.Empty, OriginalText) : (OriginalText ?? string.Empty, null);
            case GedcomDateKind.Phrase:
                return seven ? (string.Empty, Phrase) : ($"({Phrase})", null);
            case GedcomDateKind.Interpreted:
                string interpreted = Start?.Format(version) ?? string.Empty;
                return seven ? (interpreted, Phrase) : ($"INT {interpreted} ({Phrase})", null);
        }

        string start = Start?.Format(version) ?? string.Empty;
        string end = End?.Format(version) ?? string.Empty;
        string value = Kind switch
        {
            GedcomDateKind.Simple => start,
            GedcomDateKind.About => $"ABT {start}",
            GedcomDateKind.Calculated => $"CAL {start}",
            GedcomDateKind.Estimated => $"EST {start}",
            GedcomDateKind.Before => $"BEF {start}",
            GedcomDateKind.After => $"AFT {start}",
            GedcomDateKind.Between => $"BET {start} AND {end}",
            GedcomDateKind.From => $"FROM {start}",
            GedcomDateKind.To => $"TO {end}",
            GedcomDateKind.FromTo => $"FROM {start} TO {end}",
            _ => string.Empty
        };

        if (seven)
        {
            bool dual = Start?.DualYear is not null || End?.DualYear is not null;
            string? phrase = Phrase ?? (dual ? Format(GedcomVersion.V551) : null);
            return (value, phrase);
        }

        if (Phrase is not null && Kind == GedcomDateKind.Simple)
        {
            return ($"INT {value} ({Phrase})", null);
        }

        return (value, null);
    }

    /// <summary>Two dates are equal when they mean the same thing, however they were written.</summary>
    public bool Equals(GedcomDate? other) =>
        other is not null
        && Kind == other.Kind
        && Start == other.Start
        && End == other.End
        && Phrase == other.Phrase
        && (Kind != GedcomDateKind.Unparsed || OriginalText == other.OriginalText);

    public override int GetHashCode() => HashCode.Combine(Kind, Start, End, Phrase, Kind == GedcomDateKind.Unparsed ? OriginalText : null);

    /// <summary>The 7.0 form, with any phrase in brackets: <c>JULIAN 1 JAN 1700</c>, <c>ABT 1900 (family bible)</c>.</summary>
    public override string ToString()
    {
        if (Kind == GedcomDateKind.Unparsed)
        {
            return OriginalText ?? string.Empty;
        }

        var (value, phrase) = FormatWithPhrase(GedcomVersion.V70);
        return phrase is null ? value : value.Length == 0 ? $"({phrase})" : $"{value} ({phrase})";
    }
}
