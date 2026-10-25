namespace GedcomSharp.Parsing;

/// <summary>
/// The passes that need the version: undoing @@ escapes (whose rules differ between 5.x and 7.0) and
/// checking xrefs and pointers once every record is known.
/// </summary>
internal static class GedcomPostProcessor
{
    public static void Unescape(IEnumerable<GedcomStructure> roots, GedcomVersion version, GedcomIssueCollector issues)
    {
        foreach (var root in roots)
        {
            foreach (var structure in root.DescendantsAndSelf())
            {
                if (structure.Value is not { Length: > 0 } value || !value.Contains('@'))
                {
                    continue;
                }

                structure.Value = version.IsSeven
                    ? UnescapeSeven(value, structure.LineNumber, issues)
                    : value.Replace("@@", "@", StringComparison.Ordinal);
            }
        }
    }

    /// <summary>7.0 doubles only an @ that starts a line; an @ anywhere else is literal, so "a@@b" means "a@@b".</summary>
    internal static string UnescapeSeven(string value, int lineNumber, GedcomIssueCollector? issues)
    {
        var builder = new StringBuilder(value.Length);
        int lineStart = 0;
        while (lineStart <= value.Length)
        {
            int newline = value.IndexOf('\n', lineStart);
            int lineEnd = newline < 0 ? value.Length : newline;
            var line = value.AsSpan(lineStart, lineEnd - lineStart);

            if (line.StartsWith("@@"))
            {
                builder.Append(line[1..]);
            }
            else
            {
                if (line.Length > 0 && line[0] == '@')
                {
                    issues?.Warning(GedcomIssueCodes.UNESCAPED_AT, "A line value starting with a single @ should start with @@ in GEDCOM 7.", lineNumber);
                }

                builder.Append(line);
            }

            if (newline < 0)
            {
                break;
            }

            builder.Append('\n');
            lineStart = newline + 1;
        }

        return builder.ToString();
    }

    public static void CheckReferences(GedcomStructure? header, IReadOnlyList<GedcomStructure> records, GedcomVersion version, bool checkPointers, GedcomIssueCollector issues)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (record.Xref is not string xref)
            {
                continue;
            }

            if (!known.Add(xref))
            {
                issues.Warning(GedcomIssueCodes.DUPLICATE_XREF, $"@{xref}@ is used by more than one record; pointers resolve to the first.", record.LineNumber);
            }

            if (!IsValidXref(xref, version))
            {
                issues.Info(GedcomIssueCodes.INVALID_XREF, $"@{xref}@ is not a valid GEDCOM {version.ToVersionString()} cross-reference id.", record.LineNumber);
            }
        }

        if (!checkPointers)
        {
            return;
        }

        var roots = header is null ? records : records.Prepend(header);
        foreach (var root in roots)
        {
            foreach (var structure in root.Descendants())
            {
                if (structure.Pointer is string pointer && !structure.IsVoidPointer && !known.Contains(pointer))
                {
                    issues.Warning(GedcomIssueCodes.UNRESOLVED_POINTER, $"{structure.Path} points to @{pointer}@, which is not in the file.", structure.LineNumber);
                }
            }
        }
    }

    /// <summary>
    /// 7.0 allows uppercase letters, digits and underscore. 5.5.5 allows letters and digits. 5.5 and 5.5.1 only
    /// forbid @, control characters and a leading '#', and limit the id to 20 characters.
    /// </summary>
    public static bool IsValidXref(string xref, GedcomVersion version) => version switch
    {
        GedcomVersion.V70 => xref != GedcomStructure.VOID_POINTER && xref.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_'),
        GedcomVersion.V555 => xref.Length <= 20 && xref.All(char.IsAsciiLetterOrDigit),
        _ => xref.Length <= 20 && xref[0] != '#' && xref.All(c => c != '@' && !char.IsControl(c))
    };
}
