namespace GedcomSharp.Values;

public enum GedcomCalendar
{
    Gregorian,
    Julian,
    Hebrew,

    /// <summary>The French Republican calendar (<c>@#DFRENCH R@</c> in 5.x, <c>FRENCH_R</c> in 7.0).</summary>
    French,

    /// <summary>Any other calendar, such as 5.x <c>@#DROMAN@</c>/<c>@#DUNKNOWN@</c> or a 7.0 extension calendar. Its name is in <see cref="GedcomDatePart.CalendarName"/>.</summary>
    Other
}

/// <summary>Julian Day Number arithmetic for the calendars GEDCOM defines. A JDN counts days, noon to noon, from 1 January 4713 BCE (Julian).</summary>
public static class GedcomCalendarMath
{
    private static readonly string[] gregorianMonths = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] hebrewMonths = ["TSH", "CSH", "KSL", "TVT", "SHV", "ADR", "ADS", "NSN", "IYR", "SVN", "TMZ", "AAV", "ELL"];
    private static readonly string[] frenchMonths = ["VEND", "BRUM", "FRIM", "NIVO", "PLUV", "VENT", "GERM", "FLOR", "PRAI", "MESS", "THER", "FRUC", "COMP"];

    private const int HEBREW_EPOCH_RD = -1373427;
    private const int RD_TO_JDN = 1721425;
    private const int FRENCH_EPOCH_JDN = 2375840;

    /// <summary>The GEDCOM month codes for a calendar, in order (JAN..DEC, TSH..ELL, VEND..COMP).</summary>
    public static IReadOnlyList<string> GetMonthCodes(GedcomCalendar calendar) => calendar switch
    {
        GedcomCalendar.Gregorian or GedcomCalendar.Julian => gregorianMonths,
        GedcomCalendar.Hebrew => hebrewMonths,
        GedcomCalendar.French => frenchMonths,
        _ => []
    };

    public static int MonthsInYear(GedcomCalendar calendar) => calendar is GedcomCalendar.Hebrew or GedcomCalendar.French ? 13 : 12;

    /// <summary>Converts a date to its Julian Day Number. <paramref name="year"/> is astronomical (1 BCE is 0) for Gregorian and Julian.</summary>
    public static int ToJulianDay(GedcomCalendar calendar, int year, int month, int day) => calendar switch
    {
        GedcomCalendar.Gregorian => FromGregorian(year, month, day),
        GedcomCalendar.Julian => FromJulian(year, month, day),
        GedcomCalendar.Hebrew => FromHebrew(year, month, day),
        GedcomCalendar.French => FromFrench(year, month, day),
        _ => throw new ArgumentOutOfRangeException(nameof(calendar), calendar, "Day arithmetic is not defined for this calendar.")
    };

    public static int DaysInMonth(GedcomCalendar calendar, int year, int month) => calendar switch
    {
        GedcomCalendar.Gregorian or GedcomCalendar.Julian => month == 12
            ? 31
            : ToJulianDay(calendar, year, month + 1, 1) - ToJulianDay(calendar, year, month, 1),
        GedcomCalendar.Hebrew => HebrewMonthLength(year, ToCivilHebrewMonth(month)),
        GedcomCalendar.French => month < 13 ? 30 : IsFrenchLeapYear(year) ? 6 : 5,
        _ => 31
    };

    /// <summary>The first day of a year, as a JDN.</summary>
    public static int StartOfYear(GedcomCalendar calendar, int year) => calendar switch
    {
        GedcomCalendar.Hebrew => HebrewNewYear(year) + RD_TO_JDN,
        _ => ToJulianDay(calendar, year, 1, 1)
    };

    public static void ToGregorian(int julianDay, out int year, out int month, out int day)
    {
        long f = julianDay + 1401 + ((((4L * julianDay) + 274277) / 146097) * 3 / 4) - 38;
        long e = (4 * f) + 3;
        long g = e % 1461 / 4;
        long h = (5 * g) + 2;
        day = (int)((h % 153 / 5) + 1);
        month = (int)(((h / 153) + 2) % 12) + 1;
        year = (int)((e / 1461) - 4716 + ((12 + 2 - month) / 12));
    }

    private static int FromGregorian(int year, int month, int day)
    {
        int a = (14 - month) / 12;
        long y = year + 4800L - a;
        long m = month + (12 * a) - 3;
        return (int)(day + (((153 * m) + 2) / 5) + (365 * y) + FloorDiv(y, 4) - FloorDiv(y, 100) + FloorDiv(y, 400) - 32045);
    }

    private static int FromJulian(int year, int month, int day)
    {
        int a = (14 - month) / 12;
        long y = year + 4800L - a;
        long m = month + (12 * a) - 3;
        return (int)(day + (((153 * m) + 2) / 5) + (365 * y) + FloorDiv(y, 4) - 32083);
    }

    private static long FloorDiv(long value, long divisor) => (long)Math.Floor(value / (double)divisor);

    // Hebrew arithmetic follows Reingold & Dershowitz, Calendrical Calculations, with months numbered from Nisan.
    // GEDCOM numbers months from Tishri (TSH = 1 ... ELL = 13), so they are mapped first.
    private static int ToCivilHebrewMonth(int gedcomMonth) => gedcomMonth <= 7 ? gedcomMonth + 6 : gedcomMonth - 7;

    private static bool IsHebrewLeapYear(int year) => (((7L * year) + 1) % 19 + 19) % 19 < 7;

    private static int LastHebrewMonth(int year) => IsHebrewLeapYear(year) ? 13 : 12;

    private static long HebrewElapsedDays(int year)
    {
        long monthsElapsed = (long)Math.Floor(((235.0 * year) - 234) / 19);
        long partsElapsed = 12084 + (13753 * monthsElapsed);
        long days = (29 * monthsElapsed) + (long)Math.Floor(partsElapsed / 25920.0);
        return ((3 * (days + 1)) % 7 + 7) % 7 < 3 ? days + 1 : days;
    }

    private static int HebrewYearLengthCorrection(int year)
    {
        long previous = HebrewElapsedDays(year - 1);
        long current = HebrewElapsedDays(year);
        long next = HebrewElapsedDays(year + 1);
        if (next - current == 356)
        {
            return 2;
        }

        return current - previous == 382 ? 1 : 0;
    }

    private static int HebrewNewYear(int year) => (int)(HEBREW_EPOCH_RD + HebrewElapsedDays(year) + HebrewYearLengthCorrection(year));

    private static int DaysInHebrewYear(int year) => HebrewNewYear(year + 1) - HebrewNewYear(year);

    private static int HebrewMonthLength(int year, int civilMonth)
    {
        int yearLength = DaysInHebrewYear(year);
        bool longMarheshvan = yearLength is 355 or 385;
        bool shortKislev = yearLength is 353 or 383;
        if (civilMonth is 2 or 4 or 6 or 10 or 13
            || (civilMonth == 12 && !IsHebrewLeapYear(year))
            || (civilMonth == 8 && !longMarheshvan)
            || (civilMonth == 9 && shortKislev))
        {
            return 29;
        }

        return 30;
    }

    private static int FromHebrew(int year, int gedcomMonth, int day)
    {
        int month = ToCivilHebrewMonth(gedcomMonth);
        if (month == 13 && !IsHebrewLeapYear(year))
        {
            // ADS (Adar Sheni) only exists in leap years; treat it as Adar.
            month = 12;
        }

        int days = HebrewNewYear(year) + day - 1;
        if (month < 7)
        {
            for (int m = 7; m <= LastHebrewMonth(year); m++)
            {
                days += HebrewMonthLength(year, m);
            }

            for (int m = 1; m < month; m++)
            {
                days += HebrewMonthLength(year, m);
            }
        }
        else
        {
            for (int m = 7; m < month; m++)
            {
                days += HebrewMonthLength(year, m);
            }
        }

        return days + RD_TO_JDN;
    }

    /// <summary>Years 3, 7, 11 and 15 were leap years while the calendar was in use; later years follow the Romme rule.</summary>
    private static bool IsFrenchLeapYear(int year) => year < 20
        ? year is 3 or 7 or 11 or 15
        : year % 4 == 0 && (year % 100 != 0 || year % 400 == 0) && year % 4000 != 0;

    private static int FromFrench(int year, int month, int day)
    {
        int days = 0;
        for (int y = 1; y < year; y++)
        {
            days += IsFrenchLeapYear(y) ? 366 : 365;
        }

        return FRENCH_EPOCH_JDN + days + (30 * (month - 1)) + day - 1;
    }
}
