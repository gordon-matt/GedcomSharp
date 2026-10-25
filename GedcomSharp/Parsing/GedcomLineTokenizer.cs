namespace GedcomSharp.Parsing;

/// <summary>Splits decoded text into lines and each line into level, xref, tag and raw value.</summary>
/// <remarks>
/// Deliberately tolerant: leading whitespace, tabs or repeated spaces as delimiters, lowercase tags and
/// lines without a level are all accepted and reported, because real 5.x files contain all of them.
/// </remarks>
internal static class GedcomLineTokenizer
{
    private const int MAX_LEVEL = 99;

    public sealed record TokenizeResult(List<GedcomLine> Lines, string LineTerminator);

    public static TokenizeResult Tokenize(string text, GedcomReaderOptions options, GedcomIssueCollector issues)
    {
        var lines = new List<GedcomLine>(text.Length / 24);
        int crlf = 0, lf = 0, cr = 0, blank = 0, leadingWhitespace = 0, extraDelimiters = 0;
        int lineNumber = 0;
        int position = 0;

        while (position < text.Length)
        {
            int end = text.AsSpan(position).IndexOfAny('\r', '\n');
            int lineEnd = end < 0 ? text.Length : position + end;
            string raw = text[position..lineEnd];
            lineNumber++;

            if (lineEnd < text.Length)
            {
                if (text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n')
                {
                    crlf++;
                    position = lineEnd + 2;
                }
                else
                {
                    if (text[lineEnd] == '\r')
                    {
                        cr++;
                    }
                    else
                    {
                        lf++;
                    }

                    position = lineEnd + 1;
                }
            }
            else
            {
                position = lineEnd;
            }

            // Strip DOS end-of-file markers and stray NULs that some exporters leave behind.
            raw = raw.TrimEnd('\u001A', '\0');
            if (raw.Length == 0 || IsWhitespaceOnly(raw))
            {
                blank++;
                continue;
            }

            var line = ParseLine(raw, lineNumber, options, issues, ref leadingWhitespace, ref extraDelimiters);
            lines.Add(line);
        }

        // An LF CR terminator (allowed before 5.5.5) splits into LF + an empty line; that is not a real blank line.
        bool lfcr = lf > 0 && cr > 0 && Math.Abs(lf - cr) <= 1 && crlf == 0;
        if (blank > 0 && !(lfcr && blank >= cr - 1))
        {
            issues.Info(GedcomIssueCodes.BLANK_LINE, $"{blank} blank line(s) were skipped.");
        }

        if (leadingWhitespace > 0)
        {
            issues.Info(GedcomIssueCodes.LEADING_WHITESPACE, $"{leadingWhitespace} line(s) started with whitespace, which was ignored.");
        }

        if (extraDelimiters > 0)
        {
            issues.Info(GedcomIssueCodes.EXTRA_DELIMITERS, $"{extraDelimiters} line(s) used more than one space or a tab between level, xref and tag.");
        }

        string terminator = lfcr ? "\n\r" : (crlf >= lf && crlf >= cr && crlf > 0) ? "\r\n" : lf >= cr && lf > 0 ? "\n" : cr > 0 ? "\r" : "\r\n";
        int kinds = (crlf > 0 ? 1 : 0) + (lf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
        if (kinds > 1 && !lfcr)
        {
            issues.Info(GedcomIssueCodes.MIXED_LINE_TERMINATORS, $"The file mixes line terminators (CRLF {crlf}, LF {lf}, CR {cr}).");
        }

        return new TokenizeResult(lines, terminator);
    }

    private static GedcomLine ParseLine(string raw, int lineNumber, GedcomReaderOptions options, GedcomIssueCollector issues, ref int leadingWhitespace, ref int extraDelimiters)
    {
        int i = 0;
        int length = raw.Length;
        while (i < length && IsDelimiter(raw[i]))
        {
            i++;
        }

        if (i > 0)
        {
            leadingWhitespace++;
        }

        int levelStart = i;
        while (i < length && char.IsAsciiDigit(raw[i]))
        {
            i++;
        }

        if (i == levelStart || i - levelStart > 2 || i == length || !IsDelimiter(raw[i]))
        {
            // Not "<level> <tag>": usually a line break that slipped into a value.
            return new GedcomLine(lineNumber, -1, null, string.Empty, null, raw);
        }

        int level = int.Parse(raw.AsSpan(levelStart, i - levelStart), CultureInfo.InvariantCulture);
        if (level > MAX_LEVEL)
        {
            return new GedcomLine(lineNumber, -1, null, string.Empty, null, raw);
        }

        i = SkipDelimiters(raw, i, ref extraDelimiters);

        string? xref = null;
        if (i < length && raw[i] == '@')
        {
            int close = raw.IndexOf('@', i + 1);
            if (close > i && (close + 1 == length || IsDelimiter(raw[close + 1])))
            {
                xref = raw[(i + 1)..close];
                i = close + 1;
                if (xref.Length == 0)
                {
                    issues.Warning(GedcomIssueCodes.INVALID_XREF, "An empty cross-reference id (@@) was ignored.", lineNumber);
                    xref = null;
                }

                i = SkipDelimiters(raw, i, ref extraDelimiters);
            }
        }

        int tagStart = i;
        while (i < length && !IsDelimiter(raw[i]))
        {
            i++;
        }

        string tag = raw[tagStart..i];
        if (tag.Length == 0)
        {
            issues.Error(GedcomIssueCodes.MALFORMED_LINE, $"Line has no tag: '{Truncate(raw)}'.", lineNumber);
            return new GedcomLine(lineNumber, -1, null, string.Empty, null, raw);
        }

        if (!IsValidTag(tag))
        {
            issues.Warning(GedcomIssueCodes.INVALID_TAG, $"Tag '{Truncate(tag)}' contains characters other than letters, digits and underscore.", lineNumber);
        }

        if (options.NormalizeTagCase && HasLowercase(tag))
        {
            issues.Info(GedcomIssueCodes.TAG_CASE, $"Tag '{tag}' was read as '{tag.ToUpperInvariant()}'.", lineNumber);
            tag = tag.ToUpperInvariant();
        }

        string? value = null;
        if (i < length)
        {
            // Exactly one delimiter separates tag and value; anything after it, spaces included, is payload.
            value = raw[(i + 1)..];
            if (value.Length == 0)
            {
                value = null;
            }
        }

        return new GedcomLine(lineNumber, level, xref, tag, value, raw);
    }

    private static int SkipDelimiters(string raw, int index, ref int extraDelimiters)
    {
        int start = index;
        while (index < raw.Length && IsDelimiter(raw[index]))
        {
            index++;
        }

        if (index - start > 1 || (index > start && raw[start] == '\t'))
        {
            extraDelimiters++;
        }

        return index;
    }

    private static bool IsDelimiter(char value) => value is ' ' or '\t';

    private static bool IsWhitespaceOnly(string value)
    {
        foreach (char c in value)
        {
            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsValidTag(string tag)
    {
        foreach (char c in tag)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasLowercase(string tag)
    {
        foreach (char c in tag)
        {
            if (char.IsAsciiLetterLower(c))
            {
                return true;
            }
        }

        return false;
    }

    internal static string Truncate(string value) => value.Length <= 60 ? value : value[..57] + "...";
}
