using System.Text.RegularExpressions;

namespace GedcomSharp.GedcomX;

/// <summary>A point in time in the GEDCOM X formal date format: <c>+1900-01-05</c>, optionally with a time.</summary>
public sealed record GedcomXSimpleDate(int Year, int? Month = null, int? Day = null, int? Hours = null, int? Minutes = null, int? Seconds = null, string? TimeZone = null)
{
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Year < 0 ? '-' : '+').Append(Math.Abs(Year).ToString("0000", CultureInfo.InvariantCulture));
        if (Month is int month)
        {
            builder.Append('-').Append(month.ToString("00", CultureInfo.InvariantCulture));
            if (Day is int day)
            {
                builder.Append('-').Append(day.ToString("00", CultureInfo.InvariantCulture));
                if (Hours is int hours)
                {
                    builder.Append('T').Append(hours.ToString("00", CultureInfo.InvariantCulture));
                    if (Minutes is int minutes)
                    {
                        builder.Append(':').Append(minutes.ToString("00", CultureInfo.InvariantCulture));
                        if (Seconds is int seconds)
                        {
                            builder.Append(':').Append(seconds.ToString("00", CultureInfo.InvariantCulture));
                        }
                    }

                    builder.Append(TimeZone);
                }
            }
        }

        return builder.ToString();
    }
}

/// <summary>
/// A GEDCOM X formal date (Date Format specification): a simple date, a range (<c>+1900/+1910</c>, <c>/+1900</c>,
/// <c>+1900/</c>, <c>+1900/P10Y</c>), an approximate date or range (<c>A+1900</c>) or a recurrence (<c>R3/+1900/P1Y</c>).
/// Years are astronomical: <c>-0001</c> is 2 BCE.
/// </summary>
public sealed partial record GedcomXDate
{
    public bool IsApproximate { get; init; }

    public bool IsRange { get; init; }

    public bool IsRecurring { get; init; }

    /// <summary>For recurrences, how many times; null for unbounded.</summary>
    public int? Repetitions { get; init; }

    public GedcomXSimpleDate? Start { get; init; }

    public GedcomXSimpleDate? End { get; init; }

    /// <summary>An ISO 8601 duration such as <c>P10Y</c> used instead of an end date.</summary>
    public string? Duration { get; init; }

    public static GedcomXDate Simple(GedcomXSimpleDate date, bool approximate = false) => new() { Start = date, IsApproximate = approximate };

    public static GedcomXDate Range(GedcomXSimpleDate? start, GedcomXSimpleDate? end, bool approximate = false) =>
        new() { Start = start, End = end, IsRange = true, IsApproximate = approximate };

    public static bool TryParse(string? text, out GedcomXDate date)
    {
        date = new GedcomXDate();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string value = text.Trim();
        if (value.StartsWith('R'))
        {
            string[] parts = value.Split('/');
            if (parts.Length != 3 || !TryParseSimple(parts[1], out var recurringStart))
            {
                return false;
            }

            int? repetitions = parts[0].Length > 1 && int.TryParse(parts[0][1..], NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : null;
            bool isDuration = parts[2].StartsWith('P');
            GedcomXSimpleDate? recurringEnd = null;
            if (!isDuration && !TryParseSimple(parts[2], out recurringEnd))
            {
                return false;
            }

            date = new GedcomXDate
            {
                IsRecurring = true,
                IsRange = true,
                Repetitions = repetitions,
                Start = recurringStart,
                End = recurringEnd,
                Duration = isDuration ? parts[2] : null
            };
            return true;
        }

        bool approximate = value.StartsWith('A');
        if (approximate)
        {
            value = value[1..];
        }

        int slash = value.IndexOf('/');
        if (slash < 0)
        {
            if (!TryParseSimple(value, out var simple))
            {
                return false;
            }

            date = Simple(simple!, approximate);
            return true;
        }

        string left = value[..slash];
        string right = value[(slash + 1)..];
        GedcomXSimpleDate? start = null;
        GedcomXSimpleDate? end = null;
        string? duration = null;
        if (left.Length > 0 && !TryParseSimple(left, out start))
        {
            return false;
        }

        if (right.StartsWith('P'))
        {
            if (start is null || !DurationRegex().IsMatch(right))
            {
                return false;
            }

            duration = right;
        }
        else if (right.Length > 0 && !TryParseSimple(right, out end))
        {
            return false;
        }

        if (start is null && end is null)
        {
            return false;
        }

        date = new GedcomXDate { IsApproximate = approximate, IsRange = true, Start = start, End = end, Duration = duration };
        return true;
    }

    public static GedcomXDate? Parse(string? text) => TryParse(text, out var date) ? date : null;

    public override string ToString()
    {
        if (IsRecurring)
        {
            return $"R{Repetitions}/{Start}/{(object?)Duration ?? End}";
        }

        string prefix = IsApproximate ? "A" : string.Empty;
        if (!IsRange)
        {
            return prefix + Start;
        }

        return $"{prefix}{Start}/{(object?)Duration ?? End}";
    }

    private static bool TryParseSimple(string text, out GedcomXSimpleDate? date)
    {
        date = null;
        var match = SimpleDateRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        int year = int.Parse(match.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        if (match.Groups["sign"].Value == "-")
        {
            year = -year;
        }

        date = new GedcomXSimpleDate(
            year,
            Optional(match.Groups["month"]),
            Optional(match.Groups["day"]),
            Optional(match.Groups["hours"]),
            Optional(match.Groups["minutes"]),
            Optional(match.Groups["seconds"]),
            match.Groups["zone"].Success ? match.Groups["zone"].Value : null);
        return true;
    }

    private static int? Optional(System.Text.RegularExpressions.Group group) => group.Success ? int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"^(?<sign>[+-])(?<year>\d{4})(?:-(?<month>\d{2})(?:-(?<day>\d{2})(?:T(?<hours>\d{2})(?::(?<minutes>\d{2})(?::(?<seconds>\d{2}))?)?(?<zone>Z|[+-]\d{2}(?::?\d{2})?)?)?)?)?$")]
    private static partial Regex SimpleDateRegex();

    [GeneratedRegex(@"^P(?:\d+Y)?(?:\d+M)?(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+S)?)?$")]
    private static partial Regex DurationRegex();
}
