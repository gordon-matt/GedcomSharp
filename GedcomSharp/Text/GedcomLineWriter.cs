namespace GedcomSharp.Text;

/// <summary>
/// Turns structures into GEDCOM lines: escapes @ signs for the target version, writes newlines as CONT and, for
/// 5.x, splits long text with CONC so no line exceeds the limit in the target encoding.
/// </summary>
internal sealed class GedcomLineWriter(GedcomWriterOptions options, GedcomVersion version, Encoding encoding, bool countCodeUnits)
{
    private const int MAX_VALUE_LENGTH = 248;

    private readonly StringBuilder builder = new(64 * 1024);

    public int LineCount { get; private set; }

    public void Write(GedcomStructure structure, int level)
    {
        string prefix = structure.Xref is null
            ? $"{level} {structure.Tag}"
            : $"{level} @{structure.Xref}@ {structure.Tag}";

        if (structure.Pointer is string pointer)
        {
            AppendLine($"{prefix} @{pointer}@");
        }
        else if (string.IsNullOrEmpty(structure.Value))
        {
            AppendLine(prefix);
        }
        else
        {
            string[] lines = structure.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            WriteText(prefix, lines[0], level);
            for (int i = 1; i < lines.Length; i++)
            {
                WriteText($"{level + 1} {GedcomTags.CONT}", lines[i], level);
            }
        }

        foreach (var child in structure.Children)
        {
            Write(child, level + 1);
        }
    }

    public void WriteTrailer() => AppendLine($"0 {GedcomTags.TRLR}");

    public override string ToString() => builder.ToString();

    private void WriteText(string prefix, string text, int level)
    {
        if (text.Length == 0)
        {
            AppendLine(prefix);
            return;
        }

        if (version.IsSeven)
        {
            AppendLine(text[0] == '@' ? $"{prefix} @{text}" : $"{prefix} {text}");
            return;
        }

        var tokens = Tokenize(text);
        if (options.MaxLineLength <= 0)
        {
            AppendLine($"{prefix} {string.Concat(tokens.Select(token => token.Escaped))}");
            return;
        }

        string concPrefix = $"{level + 1} {GedcomTags.CONC}";
        int firstBudget = Math.Min(MAX_VALUE_LENGTH, options.MaxLineLength - Units(prefix) - 1);
        int concBudget = Math.Min(MAX_VALUE_LENGTH, options.MaxLineLength - Units(concPrefix) - 1);

        int start = 0;
        bool first = true;
        while (start < tokens.Count)
        {
            int end = TakeChunk(tokens, start, first ? firstBudget : concBudget);
            string chunk = string.Concat(tokens.Skip(start).Take(end - start).Select(token => token.Escaped));
            AppendLine($"{(first ? prefix : concPrefix)} {chunk}");
            start = end;
            first = false;
        }
    }

    /// <summary>
    /// The end of the next chunk. Splits prefer a point between two non-space characters, because readers that
    /// trim lines would otherwise lose the space, and never break a character, an @@ pair or an @#...@ escape.
    /// </summary>
    private static int TakeChunk(List<Token> tokens, int start, int budget)
    {
        int used = 0;
        int end = start;
        while (end < tokens.Count && used + tokens[end].Units <= budget)
        {
            used += tokens[end].Units;
            end++;
        }

        if (end == start)
        {
            return start + 1;
        }

        if (end == tokens.Count)
        {
            return end;
        }

        for (int split = end; split > start + 1; split--)
        {
            if (!tokens[split - 1].IsSpace && !tokens[split].IsSpace)
            {
                return split;
            }
        }

        for (int split = end; split > start + 1; split--)
        {
            if (!tokens[split].IsSpace)
            {
                return split;
            }
        }

        return end;
    }

    private readonly record struct Token(string Escaped, int Units, bool IsSpace);

    private List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>(text.Length);
        bool escape = options.EscapeAtSigns || IsPointerLike(text);
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '#')
            {
                int close = text.IndexOf('@', index + 2);
                if (close > 0)
                {
                    string sequence = text[index..(close + 1)];
                    tokens.Add(new Token(sequence, Units(sequence), false));
                    index = close + 1;
                    continue;
                }
            }

            int length = StringInfo.GetNextTextElementLength(text, index);
            string element = text.Substring(index, length);
            if (element == "@" && escape)
            {
                element = "@@";
            }

            tokens.Add(new Token(element, Units(element), element == " "));
            index += length;
        }

        return tokens;
    }

    /// <summary>Text that would read back as a pointer must be escaped even when escaping is turned off.</summary>
    private static bool IsPointerLike(string text) => Parsing.GedcomTreeBuilder.IsPointer(text);

    private int Units(string value) => countCodeUnits ? value.Length : encoding.GetByteCount(value);

    private void AppendLine(string line)
    {
        builder.Append(line).Append(options.LineTerminator);
        LineCount++;
    }
}
