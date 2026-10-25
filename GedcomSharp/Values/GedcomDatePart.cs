namespace GedcomSharp.Values;

/// <summary>A single calendar date with year, month or day precision: the <c>1 JAN 1900</c> in <c>BEF 1 JAN 1900</c>.</summary>
public sealed record GedcomDatePart
{
    public GedcomDatePart(int year, int? month = null, int? day = null, GedcomCalendar calendar = GedcomCalendar.Gregorian)
    {
        if (day is not null && month is null)
        {
            throw new ArgumentException("A day needs a month.", nameof(day));
        }

        Year = year;
        Month = month;
        Day = day;
        Calendar = calendar;
    }

    public GedcomCalendar Calendar { get; init; }

    /// <summary>The calendar as written for <see cref="GedcomCalendar.Other"/> (e.g. <c>ROMAN</c>, <c>_MAYAN</c>).</summary>
    public string? CalendarName { get; init; }

    /// <summary>The year as written. BCE years are positive with <see cref="IsBce"/> set.</summary>
    public int Year { get; init; }

    /// <summary>1-based month in the calendar's GEDCOM order (JAN = 1, TSH = 1, VEND = 1).</summary>
    public int? Month { get; init; }

    /// <summary>The month code for calendars whose months are not known to this library.</summary>
    public string? MonthCode { get; init; }

    public int? Day { get; init; }

    public bool IsBce { get; init; }

    /// <summary>
    /// The New Style year of a 5.x dual date such as <c>1699/00</c> (1700), or null. Dual dates record the
    /// period when the year started on 25 March in parts of Europe.
    /// </summary>
    public int? DualYear { get; init; }

    public bool HasDay => Day is not null;

    public bool HasMonth => Month is not null || MonthCode is not null;

    /// <summary>True for calendars this library can convert to Julian Day Numbers.</summary>
    public bool IsComputable => Calendar != GedcomCalendar.Other && Year > 0;

    /// <summary>The year used for day arithmetic: astronomical (1 BCE = 0) and New Style for dual dates.</summary>
    private int ComputationYear
    {
        get
        {
            int year = DualYear ?? Year;
            return IsBce && Calendar is GedcomCalendar.Gregorian or GedcomCalendar.Julian ? 1 - year : year;
        }
    }

    /// <summary>The first day this date could mean, as a Julian Day Number; null for unknown calendars.</summary>
    public int? MinJulianDay
    {
        get
        {
            if (!IsComputable)
            {
                return null;
            }

            int year = ComputationYear;
            if (Month is not int month)
            {
                return GedcomCalendarMath.StartOfYear(Calendar, year);
            }

            return GedcomCalendarMath.ToJulianDay(Calendar, year, month, Day ?? 1);
        }
    }

    /// <summary>The last day this date could mean, as a Julian Day Number; null for unknown calendars.</summary>
    public int? MaxJulianDay
    {
        get
        {
            if (!IsComputable)
            {
                return null;
            }

            int year = ComputationYear;
            if (Month is not int month)
            {
                return GedcomCalendarMath.StartOfYear(Calendar, year + 1) - 1;
            }

            if (Day is int day)
            {
                return GedcomCalendarMath.ToJulianDay(Calendar, year, month, day);
            }

            return GedcomCalendarMath.ToJulianDay(Calendar, year, month, 1) + GedcomCalendarMath.DaysInMonth(Calendar, year, month) - 1;
        }
    }

    /// <summary>The Gregorian <see cref="DateOnly"/> for a full date in a computable calendar, or null.</summary>
    public DateOnly? ToDateOnly()
    {
        if (!HasDay || MinJulianDay is not int julianDay)
        {
            return null;
        }

        GedcomCalendarMath.ToGregorian(julianDay, out int year, out int month, out int day);
        return year is >= 1 and <= 9999 ? new DateOnly(year, month, day) : null;
    }

    public static GedcomDatePart FromDateOnly(DateOnly date) => new(date.Year, date.Month, date.Day);

    public string? MonthText => Month is int month && GedcomCalendarMath.GetMonthCodes(Calendar) is { Count: > 0 } codes && month <= codes.Count
        ? codes[month - 1]
        : MonthCode;

    /// <summary>Writes the date in the syntax of <paramref name="version"/>.</summary>
    /// <remarks>7.0 has no dual years, so the New Style year is written; callers keep the original in a PHRASE.</remarks>
    public string Format(GedcomVersion version)
    {
        var builder = new StringBuilder(24);
        bool seven = version.IsSeven;
        string? calendar = (Calendar, seven) switch
        {
            (GedcomCalendar.Gregorian, _) => null,
            (GedcomCalendar.Julian, true) => "JULIAN",
            (GedcomCalendar.Julian, false) => "@#DJULIAN@",
            (GedcomCalendar.Hebrew, true) => "HEBREW",
            (GedcomCalendar.Hebrew, false) => "@#DHEBREW@",
            (GedcomCalendar.French, true) => "FRENCH_R",
            (GedcomCalendar.French, false) => "@#DFRENCH R@",
            (_, true) => CalendarName is { Length: > 0 } name ? (name[0] == '_' ? name : "_" + name.Replace(' ', '_')) : "_UNKNOWN",
            (_, false) => $"@#D{(CalendarName ?? "UNKNOWN").TrimStart('_')}@"
        };

        if (calendar is not null)
        {
            builder.Append(calendar).Append(' ');
        }

        if (Day is int day)
        {
            builder.Append(day.ToString(CultureInfo.InvariantCulture)).Append(' ');
        }

        if (MonthText is string month)
        {
            builder.Append(month).Append(' ');
        }

        if (seven)
        {
            builder.Append((DualYear ?? Year).ToString(CultureInfo.InvariantCulture));
            if (IsBce)
            {
                builder.Append(" BCE");
            }
        }
        else
        {
            builder.Append(Year.ToString(CultureInfo.InvariantCulture));
            if (DualYear is int dual)
            {
                builder.Append('/').Append((dual % 100).ToString("00", CultureInfo.InvariantCulture));
            }

            if (IsBce)
            {
                builder.Append(" B.C.");
            }
        }

        return builder.ToString();
    }

    public override string ToString() => Format(GedcomVersion.V70);
}
