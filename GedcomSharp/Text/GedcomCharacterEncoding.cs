namespace GedcomSharp.Text;

/// <summary>The character encodings GEDCOM files are written in, with the <c>HEAD.CHAR</c> value for each.</summary>
public enum GedcomCharacterEncoding
{
    /// <summary>UTF-8, <c>CHAR UTF-8</c>. The only encoding GEDCOM 7 allows.</summary>
    Utf8,

    /// <summary>UTF-16 little-endian, <c>CHAR UNICODE</c>. Allowed by 5.5, 5.5.1 and 5.5.5.</summary>
    Utf16LittleEndian,

    /// <summary>UTF-16 big-endian, <c>CHAR UNICODE</c>.</summary>
    Utf16BigEndian,

    /// <summary>ANSEL, <c>CHAR ANSEL</c>. The 5.5/5.5.1 default; not allowed by 5.5.5 or 7.0.</summary>
    Ansel,

    /// <summary>7-bit ASCII, <c>CHAR ASCII</c>. Characters outside ASCII are written as '?'.</summary>
    Ascii,

    /// <summary>Windows-1252, <c>CHAR ANSI</c>. Not a GEDCOM standard value, but common in old files.</summary>
    Windows1252,

    /// <summary>Any other code page detected on read, such as Windows-1251 or IBM PC (437).</summary>
    Other
}

public static class GedcomEncodings
{
    private static readonly Encoding utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static Encoding GetEncoding(GedcomCharacterEncoding encoding) => encoding switch
    {
        GedcomCharacterEncoding.Utf8 => utf8,
        GedcomCharacterEncoding.Utf16LittleEndian => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
        GedcomCharacterEncoding.Utf16BigEndian => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
        GedcomCharacterEncoding.Ansel => AnselEncoding.Instance,
        GedcomCharacterEncoding.Ascii => Encoding.GetEncoding("us-ascii", new EncoderReplacementFallback("?"), new DecoderReplacementFallback("?")),
        GedcomCharacterEncoding.Windows1252 => GetCodePage(1252)!,
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Choose a specific encoding.")
    };

    /// <summary>The <c>HEAD.CHAR</c> value written for <paramref name="encoding"/>.</summary>
    public static string GetCharacterSetName(GedcomCharacterEncoding encoding) => encoding switch
    {
        GedcomCharacterEncoding.Utf8 => "UTF-8",
        GedcomCharacterEncoding.Utf16LittleEndian or GedcomCharacterEncoding.Utf16BigEndian => "UNICODE",
        GedcomCharacterEncoding.Ansel => "ANSEL",
        GedcomCharacterEncoding.Ascii => "ASCII",
        GedcomCharacterEncoding.Windows1252 => "ANSI",
        _ => "UTF-8"
    };

    public static bool IsAllowed(GedcomCharacterEncoding encoding, GedcomVersion version) => version switch
    {
        GedcomVersion.V70 => encoding == GedcomCharacterEncoding.Utf8,
        GedcomVersion.V555 => encoding is GedcomCharacterEncoding.Utf8 or GedcomCharacterEncoding.Utf16LittleEndian or GedcomCharacterEncoding.Utf16BigEndian,
        _ => encoding != GedcomCharacterEncoding.Other
    };

    /// <summary>Returns a code page encoding (e.g. 1251, 437) without registering a global provider.</summary>
    public static Encoding? GetCodePage(int codePage)
    {
        try
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(codePage) ?? Encoding.GetEncoding(codePage);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static Encoding? GetByName(string name)
    {
        try
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(name) ?? Encoding.GetEncoding(name);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
