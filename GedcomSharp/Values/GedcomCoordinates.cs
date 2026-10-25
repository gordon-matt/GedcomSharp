using System.Text.RegularExpressions;

namespace GedcomSharp.Values;

/// <summary>A latitude or longitude as written in <c>MAP.LATI</c>/<c>MAP.LONG</c> (<c>N50.9</c>, <c>W1.8</c>), as signed degrees.</summary>
public static partial class GedcomCoordinates
{
    /// <summary>Reads <c>N50.9</c>, <c>-1.8</c>, <c>50.9N</c> or <c>50°54'30"N</c>. Returns null when the text is not a coordinate.</summary>
    public static double? Parse(string? text, bool isLatitude)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string value = text.Trim().ToUpperInvariant().Replace(',', '.');
        int sign = 1;
        char positive = isLatitude ? 'N' : 'E';
        char negative = isLatitude ? 'S' : 'W';

        if (value[0] == positive || value[0] == negative)
        {
            sign = value[0] == negative ? -1 : 1;
            value = value[1..].Trim();
        }
        else if (value[^1] == positive || value[^1] == negative)
        {
            sign = value[^1] == negative ? -1 : 1;
            value = value[..^1].Trim();
        }

        double degrees;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain))
        {
            degrees = plain;
        }
        else if (DmsRegex().Match(value) is { Success: true } dms)
        {
            degrees = double.Parse(dms.Groups["d"].Value, CultureInfo.InvariantCulture)
                + (dms.Groups["m"].Success ? double.Parse(dms.Groups["m"].Value, CultureInfo.InvariantCulture) / 60 : 0)
                + (dms.Groups["s"].Success ? double.Parse(dms.Groups["s"].Value, CultureInfo.InvariantCulture) / 3600 : 0);
        }
        else
        {
            return null;
        }

        degrees *= sign;
        double limit = isLatitude ? 90 : 180;
        return Math.Abs(degrees) <= limit ? degrees : null;
    }

    /// <summary>Writes the GEDCOM form: a hemisphere letter and decimal degrees without trailing zeros.</summary>
    public static string Format(double degrees, bool isLatitude)
    {
        char hemisphere = isLatitude ? (degrees < 0 ? 'S' : 'N') : (degrees < 0 ? 'W' : 'E');
        return hemisphere + Math.Abs(Math.Round(degrees, 6)).ToString("0.######", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^(?<d>\d+(?:\.\d+)?)\s*[°D:\s]\s*(?:(?<m>\d+(?:\.\d+)?)\s*['M:\s′]?\s*)?(?:(?<s>\d+(?:\.\d+)?)\s*(?:""|''|S|″)?)?$")]
    private static partial Regex DmsRegex();
}
