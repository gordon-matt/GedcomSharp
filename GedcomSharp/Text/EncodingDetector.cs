using System.Buffers;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace GedcomSharp.Text;

internal sealed record EncodingDetection(
    Encoding Encoding,
    GedcomCharacterEncoding Kind,
    int PreambleLength,
    bool HasByteOrderMark,
    string? DeclaredCharacterSet,
    byte[] Bytes);

/// <summary>
/// Works out how a GEDCOM file's bytes are encoded. A byte order mark wins. Otherwise the header is read as
/// ASCII (every GEDCOM 5.x encoding is ASCII-compatible in the header) and HEAD.CHAR is checked against the
/// content, because files labelled ANSEL or ASCII that are really UTF-8 or Windows-1252 are common.
/// </summary>
internal static partial class EncodingDetector
{
    private const int HEADER_SCAN_BYTES = 64 * 1024;

    public static EncodingDetection Detect(byte[] bytes, GedcomIssueCollector issues)
    {
        if (TryDetectByteOrderMark(bytes, out var bomEncoding, out var bomKind, out int bomLength))
        {
            var bomHeader = ReadHeader(bomEncoding.GetString(bytes, bomLength, Math.Min(bytes.Length - bomLength, HEADER_SCAN_BYTES)));
            string? declared = bomHeader.CharacterSet;
            if (declared is not null && !DeclarationMatches(declared, bomKind))
            {
                issues.Info(GedcomIssueCodes.ENCODING_MISMATCH, $"HEAD.CHAR is '{declared}' but the file starts with a {bomEncoding.WebName} byte order mark; the byte order mark was used.");
            }

            return new EncodingDetection(bomEncoding, bomKind, bomLength, true, declared, bytes);
        }

        // UTF-16 without a byte order mark: the first character is '0' (or a space) with a zero byte beside it.
        if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0 && bytes[0] != 0)
        {
            var encoding = GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Utf16LittleEndian);
            issues.Warning(GedcomIssueCodes.ENCODING, "The file is UTF-16 (little-endian) without a byte order mark.");
            return new EncodingDetection(encoding, GedcomCharacterEncoding.Utf16LittleEndian, 0, false, ReadHeader(encoding.GetString(bytes, 0, Math.Min(bytes.Length, HEADER_SCAN_BYTES))).CharacterSet, bytes);
        }

        if (bytes.Length >= 4 && bytes[0] == 0 && bytes[2] == 0 && bytes[1] != 0)
        {
            var encoding = GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Utf16BigEndian);
            issues.Warning(GedcomIssueCodes.ENCODING, "The file is UTF-16 (big-endian) without a byte order mark.");
            return new EncodingDetection(encoding, GedcomCharacterEncoding.Utf16BigEndian, 0, false, ReadHeader(encoding.GetString(bytes, 0, Math.Min(bytes.Length, HEADER_SCAN_BYTES))).CharacterSet, bytes);
        }

        var header = ReadHeader(Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, HEADER_SCAN_BYTES)));
        return DetectFromDeclaration(bytes, header, issues);
    }

    /// <summary>Uses a caller-supplied encoding, still skipping any byte order mark.</summary>
    public static EncodingDetection Forced(byte[] bytes, Encoding encoding)
    {
        int preamble = TryDetectByteOrderMark(bytes, out _, out _, out int bomLength) ? bomLength : 0;
        var kind = encoding switch
        {
            AnselEncoding => GedcomCharacterEncoding.Ansel,
            UTF8Encoding => GedcomCharacterEncoding.Utf8,
            UnicodeEncoding when encoding.WebName == "unicodeFFFE" => GedcomCharacterEncoding.Utf16BigEndian,
            UnicodeEncoding => GedcomCharacterEncoding.Utf16LittleEndian,
            _ when encoding.CodePage == 1252 => GedcomCharacterEncoding.Windows1252,
            _ when encoding.CodePage == 20127 => GedcomCharacterEncoding.Ascii,
            _ => GedcomCharacterEncoding.Other
        };

        return new EncodingDetection(encoding, kind, preamble, preamble > 0, null, bytes);
    }

    private static EncodingDetection DetectFromDeclaration(byte[] bytes, HeaderFacts header, GedcomIssueCollector issues)
    {
        string? declared = header.CharacterSet;
        string normalized = NormalizeName(declared);
        bool isSeven = GedcomVersionExtensions.ParseVersion(header.Version) == GedcomVersion.V70;
        bool hasHighBytes = bytes.AsSpan().IndexOfAnyInRange((byte)0x80, (byte)0xFF) >= 0;

        if (!hasHighBytes)
        {
            // Pure ASCII reads the same in every candidate encoding.
            var kind = normalized switch
            {
                "ANSEL" => GedcomCharacterEncoding.Ansel,
                "ASCII" => GedcomCharacterEncoding.Ascii,
                "ANSI" or "WINDOWS1252" or "CP1252" => GedcomCharacterEncoding.Windows1252,
                _ => GedcomCharacterEncoding.Utf8
            };

            return Create(bytes, kind, declared);
        }

        if (isSeven || normalized is "UTF8")
        {
            return DetectUtf8(bytes, declared, isSeven ? "GEDCOM 7" : "HEAD.CHAR", issues);
        }

        bool validUtf8 = Utf8.IsValid(bytes);
        switch (normalized)
        {
            case "ANSEL":
                if (validUtf8)
                {
                    issues.Warning(GedcomIssueCodes.ENCODING_MISMATCH, "HEAD.CHAR is ANSEL but the content is valid UTF-8; it was read as UTF-8.");
                    return Create(bytes, GedcomCharacterEncoding.Utf8, declared);
                }

                if (!LooksLikeAnsel(bytes))
                {
                    var guessed = GuessCodePage(bytes);
                    issues.Warning(GedcomIssueCodes.ENCODING_MISMATCH, $"HEAD.CHAR is ANSEL but the content does not follow ANSEL rules; it was read as {guessed.WebName}.");
                    return CodePage(bytes, guessed, declared);
                }

                return Create(bytes, GedcomCharacterEncoding.Ansel, declared);

            case "UNICODE" or "UTF16":
                issues.Warning(GedcomIssueCodes.ENCODING_MISMATCH, $"HEAD.CHAR is {declared} but the file is not UTF-16; it was read as {(validUtf8 ? "UTF-8" : "Windows-1252")}.");
                return Create(bytes, validUtf8 ? GedcomCharacterEncoding.Utf8 : GedcomCharacterEncoding.Windows1252, declared);
        }

        if (validUtf8)
        {
            if (declared is not null)
            {
                issues.Warning(GedcomIssueCodes.ENCODING_MISMATCH, $"HEAD.CHAR is {declared} but the content is valid UTF-8; it was read as UTF-8.");
            }

            return Create(bytes, GedcomCharacterEncoding.Utf8, declared);
        }

        if (declared is null)
        {
            issues.Warning(GedcomIssueCodes.ENCODING, "The header has no CHAR and the content is not UTF-8; it was read as Windows-1252.");
            return Create(bytes, GedcomCharacterEncoding.Windows1252, declared);
        }

        // An explicit code page under CHAR, e.g. "1 CHAR ANSI / 2 VERS 1251", is more specific than the name.
        if (int.TryParse(header.CharacterSetVersion, NumberStyles.None, CultureInfo.InvariantCulture, out int codePage)
            && GedcomEncodings.GetCodePage(codePage) is Encoding versioned)
        {
            return new EncodingDetection(versioned, codePage == 1252 ? GedcomCharacterEncoding.Windows1252 : GedcomCharacterEncoding.Other, 0, false, declared, bytes);
        }

        switch (normalized)
        {
            case "ASCII":
                issues.Warning(GedcomIssueCodes.ENCODING_MISMATCH, "HEAD.CHAR is ASCII but the file has non-ASCII bytes; it was read as Windows-1252.");
                return Create(bytes, GedcomCharacterEncoding.Windows1252, declared);
            case "ANSI" or "WINDOWS" or "IBMWINDOWS" or "WINDOWS1252" or "CP1252" or "MSWINDOWS":
                return Create(bytes, GedcomCharacterEncoding.Windows1252, declared);
            case "IBMPC" or "IBM" or "DOS" or "CP437" or "IBM437" or "OEM":
                return Other(bytes, 437, declared);
            case "CP850" or "IBM850":
                return Other(bytes, 850, declared);
            case "MACINTOSH" or "MAC" or "MACROMAN" or "APPLE":
                return Other(bytes, 10000, declared);
            case "LATIN1" or "ISO88591" or "ISOLATIN1" or "8859":
                return Other(bytes, 28591, declared);
            case "LATIN2" or "ISO88592":
                return Other(bytes, 28592, declared);
        }

        if (declared is not null && GedcomEncodings.GetByName(declared.Trim()) is Encoding named)
        {
            return new EncodingDetection(named, named.CodePage == 1252 ? GedcomCharacterEncoding.Windows1252 : GedcomCharacterEncoding.Other, 0, false, declared, bytes);
        }

        issues.Warning(GedcomIssueCodes.ENCODING, $"HEAD.CHAR '{declared}' is not a known character set; the file was read as Windows-1252.");
        return Create(bytes, GedcomCharacterEncoding.Windows1252, declared);
    }

    private static EncodingDetection DetectUtf8(byte[] bytes, string? declared, string reason, GedcomIssueCollector issues)
    {
        if (Utf8.IsValid(bytes))
        {
            return Create(bytes, GedcomCharacterEncoding.Utf8, declared);
        }

        // Some exporters (MyHeritage Family Tree Builder in particular) split a multi-byte character across a CONC.
        byte[] repaired = RepairSplitUtf8(bytes, out int repairs);
        if (repairs > 0 && Utf8.IsValid(repaired))
        {
            issues.Warning(GedcomIssueCodes.REPAIRED_UTF8, $"{repairs} UTF-8 character(s) were split across CONC lines and have been rejoined.");
            return Create(repaired, GedcomCharacterEncoding.Utf8, declared);
        }

        int invalid = CountInvalidUtf8(repairs > 0 ? repaired : bytes);
        int high = bytes.Count(b => b >= 0x80);
        if (invalid * 4 > high)
        {
            // Mostly invalid: this is really a code page file with the wrong label.
            var guessed = GuessCodePage(bytes);
            issues.Warning(GedcomIssueCodes.ENCODING_MISMATCH, $"The file should be UTF-8 ({reason}) but most non-ASCII bytes are not valid UTF-8; it was read as {guessed.WebName}.");
            return CodePage(bytes, guessed, declared);
        }

        issues.Warning(GedcomIssueCodes.INVALID_CHARACTERS, $"{invalid} invalid UTF-8 sequence(s) were replaced with U+FFFD.");
        return Create(repairs > 0 ? repaired : bytes, GedcomCharacterEncoding.Utf8, declared);
    }

    private static EncodingDetection Create(byte[] bytes, GedcomCharacterEncoding kind, string? declared) =>
        new(GedcomEncodings.GetEncoding(kind), kind, 0, false, declared, bytes);

    private static EncodingDetection CodePage(byte[] bytes, Encoding encoding, string? declared) =>
        new(encoding, encoding.CodePage == 1252 ? GedcomCharacterEncoding.Windows1252 : GedcomCharacterEncoding.Other, 0, false, declared, bytes);

    /// <summary>
    /// ANSEL puts combining marks (0xE0-0xFE) before a base letter and leaves 0x80-0xA0 and 0xC9-0xDF unused, so a
    /// mislabelled code page file (Cyrillic Windows-1251 is the usual case) breaks those rules on most of its high bytes.
    /// </summary>
    internal static bool LooksLikeAnsel(ReadOnlySpan<byte> bytes)
    {
        int high = 0;
        int implausible = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte value = bytes[i];
            if (value < 0x80)
            {
                continue;
            }

            high++;
            if (value is <= 0xA0 or (>= 0xC9 and <= 0xDF) or 0xFF)
            {
                implausible++;
            }
            else if (AnselEncoding.IsCombining(value))
            {
                int next = i + 1;
                while (next < bytes.Length && AnselEncoding.IsCombining(bytes[next]) && next - i < 3)
                {
                    next++;
                }

                if (next >= bytes.Length || (!char.IsAsciiLetter((char)bytes[next]) && bytes[next] < 0xA1))
                {
                    implausible++;
                }
            }
        }

        return implausible * 4 <= high;
    }

    /// <summary>
    /// Chooses between Windows-1251 and Windows-1252 for unlabelled 8-bit text. In 1251 every byte from 0xC0 is a
    /// Cyrillic letter, so words are runs of high bytes; Latin text in 1252 rarely has three accented letters in a row.
    /// </summary>
    internal static Encoding GuessCodePage(ReadOnlySpan<byte> bytes)
    {
        int high = 0;
        int inRuns = 0;
        int run = 0;
        foreach (byte value in bytes)
        {
            if (value >= 0xC0)
            {
                run++;
                high++;
                continue;
            }

            if (value >= 0x80)
            {
                high++;
            }

            if (run >= 3)
            {
                inRuns += run;
            }

            run = 0;
        }

        if (run >= 3)
        {
            inRuns += run;
        }

        return high > 0 && inRuns * 2 > high && GedcomEncodings.GetCodePage(1251) is Encoding cyrillic
            ? cyrillic
            : GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Windows1252);
    }

    private static EncodingDetection Other(byte[] bytes, int codePage, string? declared) =>
        new(GedcomEncodings.GetCodePage(codePage) ?? GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Windows1252), GedcomCharacterEncoding.Other, 0, false, declared, bytes);

    private static bool TryDetectByteOrderMark(byte[] bytes, out Encoding encoding, out GedcomCharacterEncoding kind, out int length)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            (encoding, kind, length) = (GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Utf8), GedcomCharacterEncoding.Utf8, 3);
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
        {
            (encoding, kind, length) = (new UTF32Encoding(bigEndian: false, byteOrderMark: false), GedcomCharacterEncoding.Other, 4);
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            (encoding, kind, length) = (new UTF32Encoding(bigEndian: true, byteOrderMark: false), GedcomCharacterEncoding.Other, 4);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            (encoding, kind, length) = (GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Utf16LittleEndian), GedcomCharacterEncoding.Utf16LittleEndian, 2);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            (encoding, kind, length) = (GedcomEncodings.GetEncoding(GedcomCharacterEncoding.Utf16BigEndian), GedcomCharacterEncoding.Utf16BigEndian, 2);
            return true;
        }

        (encoding, kind, length) = (Encoding.UTF8, GedcomCharacterEncoding.Utf8, 0);
        return false;
    }

    private static bool DeclarationMatches(string declared, GedcomCharacterEncoding kind) => NormalizeName(declared) switch
    {
        "UTF8" => kind == GedcomCharacterEncoding.Utf8,
        "UNICODE" or "UTF16" => kind is GedcomCharacterEncoding.Utf16LittleEndian or GedcomCharacterEncoding.Utf16BigEndian,
        _ => false
    };

    private static string NormalizeName(string? name) =>
        name is null ? string.Empty : NonAlphanumericRegex().Replace(name, string.Empty).ToUpperInvariant();

    /// <summary>Joins UTF-8 sequences that were cut by a line break followed by "n CONC ".</summary>
    internal static byte[] RepairSplitUtf8(byte[] bytes, out int repairs)
    {
        repairs = 0;
        var output = new List<byte>(bytes.Length);
        int i = 0;
        while (i < bytes.Length)
        {
            byte current = bytes[i];
            int needed = current switch
            {
                >= 0xC2 and <= 0xDF => 1,
                >= 0xE0 and <= 0xEF => 2,
                >= 0xF0 and <= 0xF4 => 3,
                _ => 0
            };

            if (needed == 0)
            {
                output.Add(current);
                i++;
                continue;
            }

            int have = 0;
            while (have < needed && i + 1 + have < bytes.Length && IsContinuation(bytes[i + 1 + have]))
            {
                have++;
            }

            int breakAt = i + 1 + have;
            if (have < needed && TrySkipConcPrefix(bytes, breakAt, out int resume))
            {
                int remaining = needed - have;
                bool completes = resume + remaining <= bytes.Length;
                for (int k = 0; completes && k < remaining; k++)
                {
                    completes = IsContinuation(bytes[resume + k]);
                }

                if (completes)
                {
                    for (int k = i; k < breakAt; k++)
                    {
                        output.Add(bytes[k]);
                    }

                    // The CONC line now continues the previous one directly, which is exactly what CONC means.
                    i = resume;
                    repairs++;
                    continue;
                }
            }

            for (int k = i; k < breakAt; k++)
            {
                output.Add(bytes[k]);
            }

            i = breakAt;
        }

        return [.. output];
    }

    private static bool TrySkipConcPrefix(byte[] bytes, int index, out int resume)
    {
        resume = index;
        int i = index;
        if (i >= bytes.Length || bytes[i] is not ((byte)'\r' or (byte)'\n'))
        {
            return false;
        }

        while (i < bytes.Length && bytes[i] is (byte)'\r' or (byte)'\n')
        {
            i++;
        }

        int digits = i;
        while (i < bytes.Length && bytes[i] is >= (byte)'0' and <= (byte)'9')
        {
            i++;
        }

        if (i == digits || i + 6 > bytes.Length)
        {
            return false;
        }

        if (bytes[i] != ' ' || bytes[i + 1] != 'C' || bytes[i + 2] != 'O' || bytes[i + 3] != 'N' || bytes[i + 4] != 'C' || bytes[i + 5] != ' ')
        {
            return false;
        }

        resume = i + 6;
        return true;
    }

    private static bool IsContinuation(byte value) => (value & 0xC0) == 0x80;

    private static int CountInvalidUtf8(ReadOnlySpan<byte> bytes)
    {
        int invalid = 0;
        while (!bytes.IsEmpty)
        {
            var result = Rune.DecodeFromUtf8(bytes, out _, out int consumed);
            if (result != OperationStatus.Done)
            {
                invalid++;
            }

            bytes = bytes[Math.Max(consumed, 1)..];
        }

        return invalid;
    }

    internal sealed record HeaderFacts(string? CharacterSet, string? CharacterSetVersion, string? Version);

    /// <summary>Pulls HEAD.CHAR, HEAD.CHAR.VERS and HEAD.GEDC.VERS out of the first lines of the file.</summary>
    internal static HeaderFacts ReadHeader(string text)
    {
        string? charSet = null;
        string? charVersion = null;
        string? version = null;
        string? parent = null;
        bool inHeader = false;

        foreach (string raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = HeaderLineRegex().Match(raw);
            if (!match.Success)
            {
                continue;
            }

            string level = match.Groups[1].Value;
            string tag = match.Groups[2].Value.ToUpperInvariant();
            string value = match.Groups[3].Value.Trim();

            if (level == "0")
            {
                if (inHeader)
                {
                    break;
                }

                inHeader = tag == GedcomTags.HEAD;
                continue;
            }

            if (!inHeader)
            {
                continue;
            }

            if (level == "1")
            {
                parent = tag;
                if (tag == GedcomTags.CHAR)
                {
                    charSet = value;
                }
            }
            else if (level == "2" && tag == GedcomTags.VERS)
            {
                if (parent == GedcomTags.CHAR)
                {
                    charVersion = value;
                }
                else if (parent == GedcomTags.GEDC)
                {
                    version = value;
                }
            }
        }

        return new HeaderFacts(string.IsNullOrEmpty(charSet) ? null : charSet, charVersion, version);
    }

    [GeneratedRegex(@"^\s*(\d+)\s+(?:@[^@]*@\s+)?([A-Za-z0-9_]+)(?:[ \t](.*))?$")]
    private static partial Regex HeaderLineRegex();

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumericRegex();
}
