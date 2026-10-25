namespace GedcomSharp.Text;

/// <summary>
/// ANSEL (ANSI Z39.47) as used by GEDCOM, built from the consolidated "LANSEL" table in the GEDCOM 5.5.5
/// specification: the ANSEL standard plus the LDS extensions, with MARC-8's later eszett and euro accepted on read.
/// </summary>
/// <remarks>
/// ANSEL writes combining marks before their base letter; Unicode writes them after. Decoding moves the marks
/// behind the letter and normalises to NFC. Encoding decomposes to NFD, keeps ANSEL's precomposed horn letters,
/// moves the marks back in front, and writes '?' for anything ANSEL cannot represent (one '?' per character,
/// marks included).
/// </remarks>
public sealed class AnselEncoding : Encoding
{
    public static readonly AnselEncoding Instance = new();

    private const char REPLACEMENT = '\uFFFD';

    private static readonly char[] decodeTable = BuildDecodeTable();
    private static readonly Dictionary<char, byte> encodeTable = BuildEncodeTable();

    public override string EncodingName => "ANSEL (GEDCOM)";

    public override string WebName => "x-gedcom-ansel";

    public override bool IsSingleByte => false;

    public override int GetByteCount(char[] chars, int index, int count) => EncodeCore(new string(chars, index, count)).Length;

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        byte[] encoded = EncodeCore(new string(chars, charIndex, charCount));
        encoded.CopyTo(bytes, byteIndex);
        return encoded.Length;
    }

    public override int GetCharCount(byte[] bytes, int index, int count) => DecodeCore(bytes.AsSpan(index, count)).Length;

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        string decoded = DecodeCore(bytes.AsSpan(byteIndex, byteCount));
        decoded.CopyTo(0, chars, charIndex, decoded.Length);
        return decoded.Length;
    }

    public override string GetString(byte[] bytes, int index, int count) => DecodeCore(bytes.AsSpan(index, count));

    public override byte[] GetBytes(string s) => EncodeCore(s);

    // Combining marks can make one character several bytes, and normalisation can merge characters.
    public override int GetMaxByteCount(int charCount) => (charCount + 1) * 4;

    public override int GetMaxCharCount(int byteCount) => byteCount * 2;

    public static bool IsCombining(byte value) => value is >= 0xE0 and <= 0xFE && value != 0xFD;

    internal static string DecodeCore(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        var pendingMarks = new StringBuilder();
        bool hasMarks = false;

        foreach (byte value in bytes)
        {
            char mapped = decodeTable[value];
            if (IsCombining(value))
            {
                pendingMarks.Append(mapped);
                hasMarks = true;
                continue;
            }

            if (pendingMarks.Length > 0 && (value is 0x0A or 0x0D))
            {
                // A mark with no letter after it on the same line; keep it rather than attach it to the next line.
                builder.Append(pendingMarks);
                pendingMarks.Clear();
            }

            builder.Append(mapped);
            if (pendingMarks.Length > 0)
            {
                // Same mark order as MARC-8 to Unicode conversion: the marks move behind the letter, in sequence.
                builder.Append(pendingMarks);
                pendingMarks.Clear();
            }
        }

        builder.Append(pendingMarks);
        string result = builder.ToString();
        return hasMarks ? result.Normalize(NormalizationForm.FormC) : result;
    }

    internal static byte[] EncodeCore(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        var output = new List<byte>(decomposed.Length + 8);
        int i = 0;
        while (i < decomposed.Length)
        {
            char baseChar = decomposed[i];
            int next = i + 1;

            // Surrogate pairs are never representable.
            if (char.IsHighSurrogate(baseChar) && next < decomposed.Length && char.IsLowSurrogate(decomposed[next]))
            {
                next++;
                while (next < decomposed.Length && IsCombiningMark(decomposed[next]))
                {
                    next++;
                }

                output.Add((byte)'?');
                i = next;
                continue;
            }

            var marks = new List<char>();
            while (next < decomposed.Length && IsCombiningMark(decomposed[next]))
            {
                marks.Add(decomposed[next]);
                next++;
            }

            // ANSEL has O and U with horn as single characters, but no combining horn.
            int horn = marks.IndexOf('\u031B');
            if (horn >= 0 && baseChar is 'O' or 'o' or 'U' or 'u')
            {
                baseChar = baseChar switch
                {
                    'O' => '\u01A0',
                    'o' => '\u01A1',
                    'U' => '\u01AF',
                    _ => '\u01B0'
                };
                marks.RemoveAt(horn);
            }

            if (!TryEncodeChar(baseChar, out byte baseByte))
            {
                output.Add((byte)'?');
                i = next;
                continue;
            }

            // Unsupported marks are dropped and the letter kept, as the 5.5.5 conversion notes recommend.
            foreach (char mark in marks)
            {
                if (encodeTable.TryGetValue(mark, out byte markByte) && IsCombining(markByte))
                {
                    output.Add(markByte);
                }
            }

            output.Add(baseByte);
            i = next;
        }

        return [.. output];
    }

    private static bool TryEncodeChar(char value, out byte encoded)
    {
        if (value < 0x80)
        {
            encoded = (byte)value;
            return true;
        }

        return encodeTable.TryGetValue(value, out encoded) && !IsCombining(encoded);
    }

    private static bool IsCombiningMark(char value) =>
        CharUnicodeInfo.GetUnicodeCategory(value) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark;

    private static char[] BuildDecodeTable()
    {
        var table = new char[256];
        for (int i = 0; i < 0x80; i++)
        {
            table[i] = (char)i;
        }

        for (int i = 0x80; i < 0x100; i++)
        {
            table[i] = REPLACEMENT;
        }

        // Control characters used by MARC-8: non-sorting begin/end, joiner and non-joiner.
        table[0x88] = '\u0098';
        table[0x89] = '\u009C';
        table[0x8D] = '\u200D';
        table[0x8E] = '\u200C';

        (int Code, char Value)[] spacing =
        [
            (0xA1, '\u0141'), (0xA2, '\u00D8'), (0xA3, '\u0110'), (0xA4, '\u00DE'), (0xA5, '\u00C6'), (0xA6, '\u0152'),
            (0xA7, '\u02B9'), (0xA8, '\u00B7'), (0xA9, '\u266D'), (0xAA, '\u00AE'), (0xAB, '\u00B1'), (0xAC, '\u01A0'),
            (0xAD, '\u01AF'), (0xAE, '\u02BC'), (0xB0, '\u02BB'), (0xB1, '\u0142'), (0xB2, '\u00F8'), (0xB3, '\u0111'),
            (0xB4, '\u00FE'), (0xB5, '\u00E6'), (0xB6, '\u0153'), (0xB7, '\u02BA'), (0xB8, '\u0131'), (0xB9, '\u00A3'),
            (0xBA, '\u00F0'), (0xBC, '\u01A1'), (0xBD, '\u01B0'), (0xBE, '\u25A1'), (0xBF, '\u25A0'), (0xC0, '\u00B0'),
            (0xC1, '\u2113'), (0xC2, '\u2117'), (0xC3, '\u00A9'), (0xC4, '\u266F'), (0xC5, '\u00BF'), (0xC6, '\u00A1'),
            (0xC7, '\u00DF'), (0xC8, '\u20AC'), (0xCD, 'e'), (0xCE, 'o'), (0xCF, '\u00DF')
        ];

        (int Code, char Value)[] combining =
        [
            (0xE0, '\u0309'), (0xE1, '\u0300'), (0xE2, '\u0301'), (0xE3, '\u0302'), (0xE4, '\u0303'), (0xE5, '\u0304'),
            (0xE6, '\u0306'), (0xE7, '\u0307'), (0xE8, '\u0308'), (0xE9, '\u030C'), (0xEA, '\u030A'), (0xEB, '\uFE20'),
            (0xEC, '\uFE21'), (0xED, '\u0315'), (0xEE, '\u030B'), (0xEF, '\u0310'), (0xF0, '\u0327'), (0xF1, '\u0328'),
            (0xF2, '\u0323'), (0xF3, '\u0324'), (0xF4, '\u0325'), (0xF5, '\u0333'), (0xF6, '\u0332'), (0xF7, '\u0326'),
            (0xF8, '\u031C'), (0xF9, '\u032E'), (0xFA, '\uFE22'), (0xFB, '\uFE23'), (0xFC, '\u0338'), (0xFE, '\u0313')
        ];

        foreach (var (code, value) in spacing.Concat(combining))
        {
            table[code] = value;
        }

        return table;
    }

    private static Dictionary<char, byte> BuildEncodeTable()
    {
        var table = new Dictionary<char, byte>();
        for (int i = 0xA0; i < 0x100; i++)
        {
            char value = decodeTable[i];
            // The LDS 'e'/'o' and the MARC-8 eszett duplicate are read-only aliases.
            if (value == REPLACEMENT || value < 0x80 || i == 0xC7)
            {
                continue;
            }

            table.TryAdd(value, (byte)i);
        }

        table['\u00DF'] = 0xCF;
        table['\u200D'] = 0x8D;
        table['\u200C'] = 0x8E;

        // Close matches for marks Unicode spells differently.
        table.TryAdd('\u0313', 0xFE);
        table.TryAdd('\u0312', 0xED);
        table.TryAdd('\u0331', 0xF6);
        return table;
    }
}
