using System.Text.RegularExpressions;

namespace GedcomSharp.Values;

/// <summary>Lenient parser for GEDCOM 5.x and 7.0 date values and the common ways people and exporters bend them.</summary>
internal static partial class GedcomDateParser
{
    private static readonly Dictionary<string, GedcomDateKind> singleDatePrefixes = new(StringComparer.Ordinal)
    {
        ["ABT"] = GedcomDateKind.About,
        ["ABOUT"] = GedcomDateKind.About,
        ["CIRCA"] = GedcomDateKind.About,
        ["CA"] = GedcomDateKind.About,
        ["C"] = GedcomDateKind.About,
        ["CIR"] = GedcomDateKind.About,
        ["APPROX"] = GedcomDateKind.About,
        ["APPROXIMATELY"] = GedcomDateKind.About,
        ["AROUND"] = GedcomDateKind.About,
        ["~"] = GedcomDateKind.About,
        ["UM"] = GedcomDateKind.About,
        ["ETWA"] = GedcomDateKind.About,
        ["ENV"] = GedcomDateKind.About,
        ["ENVIRON"] = GedcomDateKind.About,
        ["VERS"] = GedcomDateKind.About,
        ["OMSTREEKS"] = GedcomDateKind.About,
        ["OMSTR"] = GedcomDateKind.About,
        ["HACIA"] = GedcomDateKind.About,
        ["CAL"] = GedcomDateKind.Calculated,
        ["CALC"] = GedcomDateKind.Calculated,
        ["CALCULATED"] = GedcomDateKind.Calculated,
        ["EST"] = GedcomDateKind.Estimated,
        ["ESTIMATED"] = GedcomDateKind.Estimated,
        ["BEF"] = GedcomDateKind.Before,
        ["BEFORE"] = GedcomDateKind.Before,
        ["<"] = GedcomDateKind.Before,
        ["VOR"] = GedcomDateKind.Before,
        ["AVANT"] = GedcomDateKind.Before,
        ["AV"] = GedcomDateKind.Before,
        ["VOOR"] = GedcomDateKind.Before,
        ["ANTES"] = GedcomDateKind.Before,
        ["AFT"] = GedcomDateKind.After,
        ["AFTER"] = GedcomDateKind.After,
        [">"] = GedcomDateKind.After,
        ["NACH"] = GedcomDateKind.After,
        ["APRES"] = GedcomDateKind.After,
        ["NA"] = GedcomDateKind.After,
        ["DESPUES"] = GedcomDateKind.After
    };

    private static readonly HashSet<string> hyphenSeparator = ["-"];
    private static readonly HashSet<string> standardPrefixes = ["ABT", "CAL", "EST", "BEF", "AFT"];
    private static readonly HashSet<string> betweenWords = ["BET", "BETWEEN", "BTW", "ZWISCHEN", "ENTRE", "TUSSEN"];
    private static readonly HashSet<string> andWords = ["AND", "&", "-", "UND", "ET", "EN", "Y"];
    private static readonly HashSet<string> fromWords = ["FROM", "VON", "VAN", "DESDE"];
    private static readonly HashSet<string> toWords = ["TO", "BIS", "TOT", "HASTA", "TILL", "UNTIL"];
    private static readonly HashSet<string> bceSuffixes = ["BCE", "B.C.", "BC", "B.C.E.", "V.CHR.", "AC", "A.C.", "AV.J.-C."];
    private static readonly Dictionary<string, int> gregorianMonthNames = BuildMonthNames();

    public static GedcomDate Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return GedcomDate.Empty;
        }

        string original = text;
        string value = WhitespaceRegex().Replace(text.Trim(), " ");

        if (value.Length >= 2 && value[0] == '(' && value[^1] == ')')
        {
            return new GedcomDate { Kind = GedcomDateKind.Phrase, Phrase = value[1..^1].Trim(), OriginalText = original };
        }

        string? phrase = null;
        int open = value.IndexOf('(');
        if (open > 0 && value[^1] == ')')
        {
            phrase = value[(open + 1)..^1].Trim();
            value = value[..open].TrimEnd();
        }

        string[] words = value.Split(' ');
        string first = Key(words[0]);
        bool interpreted = first is "INT" or "INTERPRETED";
        if (interpreted)
        {
            value = string.Join(' ', words[1..]);
        }

        var body = ParseBody(value, out bool normalized);
        if (body is null)
        {
            return new GedcomDate { Kind = GedcomDateKind.Unparsed, OriginalText = original };
        }

        if (phrase is not null)
        {
            normalized |= !interpreted || body.Kind != GedcomDateKind.Simple;
            body = body.Kind == GedcomDateKind.Simple
                ? body with { Kind = GedcomDateKind.Interpreted, Phrase = phrase }
                : body with { Phrase = phrase };
        }
        else if (interpreted)
        {
            normalized = true;
        }

        return body with { OriginalText = original, WasNormalized = normalized };
    }

    private static GedcomDate? ParseBody(string value, out bool normalized)
    {
        normalized = false;
        if (value.Length == 0)
        {
            return null;
        }

        value = GluedPrefixRegex().Replace(value, "${prefix} ");
        string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string key = Key(words[0]);
        string rest = string.Join(' ', words[1..]);

        if (singleDatePrefixes.TryGetValue(key, out var kind) && rest.Length > 0)
        {
            var part = ParsePart(rest, out normalized);
            normalized |= !standardPrefixes.Contains(key) || words[0] != key;
            return part is null ? null : GedcomDate.Create(kind, part);
        }

        if (betweenWords.Contains(key) && words.Length > 1)
        {
            string[] rangeWords = words.Length == 2 && YearRangeRegex().Match(words[1]) is { Success: true } years
                ? [years.Groups[1].Value, "-", years.Groups[2].Value]
                : words[1..];
            var range = ParseRange(rangeWords, andWords, out normalized);
            normalized |= words[0] != "BET";
            return range is null ? null : GedcomDate.Between(range.Value.Start, range.Value.End);
        }

        if (fromWords.Contains(key) && words.Length > 1)
        {
            int toIndex = Array.FindIndex(words, 1, word => toWords.Contains(Key(word)));
            normalized = words[0] != "FROM" || (toIndex > 0 && words[toIndex] != "TO");
            if (toIndex < 0)
            {
                var start = ParsePart(rest, out bool partNormalized);
                normalized |= partNormalized;
                return start is null ? null : GedcomDate.Create(GedcomDateKind.From, start);
            }

            var from = ParsePart(string.Join(' ', words[1..toIndex]), out bool fromNormalized);
            var to = ParsePart(string.Join(' ', words[(toIndex + 1)..]), out bool toNormalized);
            normalized |= fromNormalized || toNormalized;
            return from is null || to is null ? null : GedcomDate.Period(from, to);
        }

        if (toWords.Contains(key) && words.Length > 1)
        {
            var end = ParsePart(rest, out normalized);
            normalized |= words[0] != "TO";
            return end is null ? null : GedcomDate.Create(GedcomDateKind.To, end);
        }

        if (value.EndsWith('?'))
        {
            var guessed = ParsePart(value.TrimEnd('?').TrimEnd(), out _);
            normalized = true;
            return guessed is null ? null : GedcomDate.Create(GedcomDateKind.About, guessed);
        }

        var simple = ParsePart(value, out normalized);
        if (simple is not null)
        {
            return GedcomDate.Create(GedcomDateKind.Simple, simple);
        }

        // "1900-1910", "1900 - 1910" or "1 JAN 1900 - 5 MAR 1900".
        var hyphenated = value.Contains(" - ", StringComparison.Ordinal)
            ? ParseRange(value.Split(' ', StringSplitOptions.RemoveEmptyEntries), hyphenSeparator, out _)
            : YearRangeRegex().Match(value) is { Success: true } match
                ? ParseRange([match.Groups[1].Value, "-", match.Groups[2].Value], hyphenSeparator, out _)
                : null;

        normalized = true;
        return hyphenated is null ? null : GedcomDate.Between(hyphenated.Value.Start, hyphenated.Value.End);
    }

    private static (GedcomDatePart Start, GedcomDatePart End)? ParseRange(string[] words, IReadOnlySet<string> separators, out bool normalized)
    {
        normalized = false;
        int separator = Array.FindIndex(words, word => separators.Contains(Key(word)));
        if (separator <= 0 || separator == words.Length - 1)
        {
            return null;
        }

        normalized = words[separator] != "AND";
        var start = ParsePart(string.Join(' ', words[..separator]), out bool startNormalized);
        var end = ParsePart(string.Join(' ', words[(separator + 1)..]), out bool endNormalized);
        normalized |= startNormalized || endNormalized;
        return start is null || end is null ? null : (start, end);
    }

    internal static GedcomDatePart? ParsePart(string text, out bool normalized)
    {
        normalized = false;
        string value = text.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        var calendar = GedcomCalendar.Gregorian;
        string? calendarName = null;

        if (value.StartsWith("@#D", StringComparison.OrdinalIgnoreCase))
        {
            int close = value.IndexOf('@', 3);
            if (close < 0)
            {
                return null;
            }

            calendarName = value[3..close].Trim().ToUpperInvariant();
            value = value[(close + 1)..].Trim();
            calendar = calendarName switch
            {
                "GREGORIAN" => GedcomCalendar.Gregorian,
                "JULIAN" => GedcomCalendar.Julian,
                "HEBREW" => GedcomCalendar.Hebrew,
                "FRENCH R" or "FRENCH" or "FRENCH_R" => GedcomCalendar.French,
                _ => GedcomCalendar.Other
            };
        }
        else
        {
            int space = value.IndexOf(' ');
            string firstWord = (space < 0 ? value : value[..space]).ToUpperInvariant();
            GedcomCalendar? keyword = firstWord switch
            {
                "GREGORIAN" => GedcomCalendar.Gregorian,
                "JULIAN" => GedcomCalendar.Julian,
                "HEBREW" => GedcomCalendar.Hebrew,
                "FRENCH_R" => GedcomCalendar.French,
                _ when firstWord.Length > 1 && firstWord[0] == '_' => GedcomCalendar.Other,
                _ => null
            };

            if (keyword is GedcomCalendar found && space > 0)
            {
                calendar = found;
                calendarName = firstWord;
                value = value[(space + 1)..].Trim();
            }
        }

        if (calendar != GedcomCalendar.Other)
        {
            calendarName = null;
        }

        bool bce = false;
        int lastSpace = value.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            string suffix = value[(lastSpace + 1)..].ToUpperInvariant();
            if (bceSuffixes.Contains(suffix))
            {
                bce = true;
                normalized |= suffix is not ("B.C." or "BCE");
                value = value[..lastSpace].TrimEnd();
            }
            else if (value.EndsWith(" v. Chr.", StringComparison.OrdinalIgnoreCase))
            {
                bce = true;
                normalized = true;
                value = value[..^8].TrimEnd();
            }
        }

        if (value.Length > 1 && value[0] == '-' && char.IsAsciiDigit(value[1]) && !value.Contains(' '))
        {
            bce = true;
            normalized = true;
            value = value[1..];
        }

        GedcomDatePart? part = null;
        if (IsoDateRegex().Match(value) is { Success: true } iso)
        {
            normalized = true;
            part = Build(calendar, calendarName, Int(iso.Groups["year"].Value), null, Int(iso.Groups["month"].Value), null, OptionalInt(iso.Groups["day"]), bce);
        }
        else if (DottedDateRegex().Match(value) is { Success: true } dotted)
        {
            normalized = true;
            part = Build(calendar, calendarName, Int(dotted.Groups["year"].Value), null, Int(dotted.Groups["month"].Value), null, Int(dotted.Groups["day"].Value), bce);
        }
        else
        {
            string[] tokens = value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            normalized |= value.Contains(',');
            part = tokens.Length switch
            {
                1 => ParseTokens(calendar, calendarName, null, null, tokens[0], bce, ref normalized),
                2 => ParseTokens(calendar, calendarName, null, tokens[0], tokens[1], bce, ref normalized),
                3 when IsDayToken(tokens[0]) => ParseTokens(calendar, calendarName, tokens[0], tokens[1], tokens[2], bce, ref normalized),
                3 when IsDayToken(tokens[1]) => ParseMonthFirst(calendar, calendarName, tokens, bce, ref normalized),
                _ => null
            };
        }

        return part;
    }

    private static GedcomDatePart? ParseMonthFirst(GedcomCalendar calendar, string? calendarName, string[] tokens, bool bce, ref bool normalized)
    {
        normalized = true;
        return ParseTokens(calendar, calendarName, tokens[1], tokens[0], tokens[2], bce, ref normalized);
    }

    private static GedcomDatePart? ParseTokens(GedcomCalendar calendar, string? calendarName, string? dayToken, string? monthToken, string yearToken, bool bce, ref bool normalized)
    {
        var yearMatch = YearRegex().Match(yearToken);
        if (!yearMatch.Success)
        {
            return null;
        }

        int year = Int(yearMatch.Groups["year"].Value);
        int? dual = null;
        if (yearMatch.Groups["dual"].Success)
        {
            dual = ResolveDualYear(year, yearMatch.Groups["dual"].Value);
            if (dual is null)
            {
                return null;
            }
        }

        int? month = null;
        string? monthCode = null;
        if (monthToken is not null)
        {
            if (!TryParseMonth(calendar, monthToken, out month, out monthCode, out bool monthNormalized))
            {
                return null;
            }

            normalized |= monthNormalized;
        }

        int? day = null;
        if (dayToken is not null)
        {
            string trimmed = OrdinalSuffixRegex().Replace(dayToken, string.Empty).TrimEnd('.');
            normalized |= trimmed != dayToken;
            if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedDay))
            {
                return null;
            }

            day = parsedDay;
        }

        return Build(calendar, calendarName, year, dual, month, monthCode, day, bce);
    }

    private static GedcomDatePart? Build(GedcomCalendar calendar, string? calendarName, int year, int? dual, int? month, string? monthCode, int? day, bool bce)
    {
        if (year <= 0 && calendar != GedcomCalendar.Other)
        {
            return null;
        }

        if (month is int m)
        {
            if (m < 1 || m > GedcomCalendarMath.MonthsInYear(calendar))
            {
                return null;
            }

            if (day is int d && (d < 1 || d > GedcomCalendarMath.DaysInMonth(calendar, bce && calendar is GedcomCalendar.Gregorian or GedcomCalendar.Julian ? 1 - year : year, m)))
            {
                return null;
            }
        }
        else if (day is not null && monthCode is null)
        {
            return null;
        }

        return new GedcomDatePart(year, month, month is null ? null : day, calendar)
        {
            CalendarName = calendarName,
            MonthCode = monthCode,
            Day = day,
            DualYear = dual,
            IsBce = bce
        };
    }

    private static bool TryParseMonth(GedcomCalendar calendar, string token, out int? month, out string? monthCode, out bool normalized)
    {
        month = null;
        monthCode = null;
        normalized = false;
        string upper = token.ToUpperInvariant();

        switch (calendar)
        {
            case GedcomCalendar.Gregorian or GedcomCalendar.Julian:
                var codes = GedcomCalendarMath.GetMonthCodes(calendar);
                int index = IndexOf(codes, upper);
                if (index >= 0)
                {
                    month = index + 1;
                    normalized = token != upper;
                    return true;
                }

                if (gregorianMonthNames.TryGetValue(RemoveDiacritics(upper).TrimEnd('.'), out int named))
                {
                    month = named;
                    normalized = true;
                    return true;
                }

                return false;

            case GedcomCalendar.Hebrew or GedcomCalendar.French:
                int found = IndexOf(GedcomCalendarMath.GetMonthCodes(calendar), upper);
                if (found < 0)
                {
                    return false;
                }

                month = found + 1;
                normalized = token != upper;
                return true;

            default:
                if (!upper.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                {
                    return false;
                }

                monthCode = upper;
                return true;
        }
    }

    private static int IndexOf(IReadOnlyList<string> codes, string value)
    {
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>"1699/00" means 1699 Old Style, 1700 New Style: the digits after the slash replace the year's last digits.</summary>
    private static int? ResolveDualYear(int year, string digits)
    {
        int value = Int(digits);
        if (digits.Length >= year.ToString(CultureInfo.InvariantCulture).Length)
        {
            return value > year ? value : null;
        }

        int scale = (int)Math.Pow(10, digits.Length);
        int candidate = year - (year % scale) + value;
        if (candidate <= year)
        {
            candidate += scale;
        }

        return candidate;
    }

    private static bool IsDayToken(string token) => DayTokenRegex().IsMatch(token);

    private static int Int(string value) => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static int? OptionalInt(Group group) => group.Success ? Int(group.Value) : null;

    private static string Key(string word) => RemoveDiacritics(word.ToUpperInvariant()).TrimEnd('.', ':', ',');

    private static string RemoveDiacritics(string value)
    {
        if (value.All(char.IsAscii))
        {
            return value;
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, int> BuildMonthNames()
    {
        string[][] names =
        [
            ["JANUARY", "JANUAR", "JANNER", "JANVIER", "JANUARI", "ENERO", "GENNAIO"],
            ["FEBRUARY", "FEBRUAR", "FEVRIER", "FEBRUARI", "FEBRERO", "FEBBRAIO"],
            ["MARCH", "MARZ", "MAERZ", "MARS", "MAART", "MARZO", "MRZ"],
            ["APRIL", "AVRIL", "ABRIL", "APRILE"],
            ["MAY", "MAI", "MEI", "MAYO", "MAGGIO"],
            ["JUNE", "JUNI", "JUIN", "JUNIO", "GIUGNO"],
            ["JULY", "JULI", "JUILLET", "JULIO", "LUGLIO"],
            ["AUGUST", "AOUT", "AUGUSTUS", "AGOSTO"],
            ["SEPTEMBER", "SEPTEMBRE", "SEPTIEMBRE", "SETIEMBRE", "SETTEMBRE"],
            ["OCTOBER", "OKTOBER", "OCTOBRE", "OCTUBRE", "OTTOBRE"],
            ["NOVEMBER", "NOVEMBRE", "NOVIEMBRE"],
            ["DECEMBER", "DEZEMBER", "DECEMBRE", "DICIEMBRE", "DICEMBRE"]
        ];

        int[] abbreviationLengths = [3, 4];
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        for (int month = 0; month < names.Length; month++)
        {
            foreach (string name in names[month])
            {
                result[name] = month + 1;
                foreach (int length in abbreviationLengths)
                {
                    if (name.Length <= length)
                    {
                        continue;
                    }

                    string abbreviation = name[..length];
                    if (result.TryGetValue(abbreviation, out int existing) && existing != month + 1)
                    {
                        ambiguous.Add(abbreviation);
                    }
                    else
                    {
                        result.TryAdd(abbreviation, month + 1);
                    }
                }
            }
        }

        foreach (string abbreviation in ambiguous)
        {
            result.Remove(abbreviation);
        }

        result["SEPT"] = 9;
        result["OKT"] = 10;
        result["DEZ"] = 12;
        return result;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?<prefix>[~<>]|[Cc][Aa]?\.)(?=\d)")]
    private static partial Regex GluedPrefixRegex();

    [GeneratedRegex(@"^(?<year>\d{3,4})-(?<month>\d{1,2})(?:-(?<day>\d{1,2}))?$")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"^(?<day>\d{1,2})\.(?<month>\d{1,2})\.(?<year>\d{3,4})$")]
    private static partial Regex DottedDateRegex();

    [GeneratedRegex(@"^(?<year>\d{1,5})(?:/(?<dual>\d{1,4}))?$")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^\d{1,2}(?:st|nd|rd|th|ST|ND|RD|TH|\.)?$")]
    private static partial Regex DayTokenRegex();

    [GeneratedRegex(@"(?<=\d)(st|nd|rd|th|ST|ND|RD|TH)$")]
    private static partial Regex OrdinalSuffixRegex();

    [GeneratedRegex(@"^(\d{3,4})\s*-\s*(\d{3,4})$")]
    private static partial Regex YearRangeRegex();
}
