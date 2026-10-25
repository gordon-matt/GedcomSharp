namespace GedcomSharp.Parsing;

/// <summary>Builds the structure tree from tokenized lines, joining CONC/CONT and recovering from broken nesting.</summary>
internal static class GedcomTreeBuilder
{
    public sealed record BuildResult(GedcomStructure? Header, List<GedcomStructure> Records, bool HasTrailer);

    public static BuildResult Build(List<GedcomLine> lines, GedcomReaderOptions options, GedcomIssueCollector issues)
    {
        GedcomStructure? header = null;
        var records = new List<GedcomStructure>();
        var stack = new List<GedcomStructure>(16);
        GedcomStructure? last = null;
        bool trailer = false;
        int afterTrailer = 0;
        int firstAfterTrailer = 0;

        foreach (var line in lines)
        {
            if (trailer)
            {
                if (afterTrailer++ == 0)
                {
                    firstAfterTrailer = line.LineNumber;
                }

                continue;
            }

            if (!line.HasLevel)
            {
                if (last is null)
                {
                    issues.Error(GedcomIssueCodes.MALFORMED_LINE, $"Line '{GedcomLineTokenizer.Truncate(line.RawText)}' comes before the first record.", line.LineNumber);
                }
                else if (options.RecoverLinesWithoutLevel && !last.HasPointer)
                {
                    // A newline typed into a value that the exporter never turned into CONT.
                    last.Value = (last.Value ?? string.Empty) + "\n" + line.RawText;
                    issues.Warning(GedcomIssueCodes.MISSING_LEVEL, "Line has no level number; it was added to the previous line's text as a new line.", line.LineNumber);
                }
                else
                {
                    issues.Error(GedcomIssueCodes.MISSING_LEVEL, $"Line has no level number and was skipped: '{GedcomLineTokenizer.Truncate(line.RawText)}'.", line.LineNumber);
                }

                continue;
            }

            if (line.Tag is GedcomTags.CONC or GedcomTags.CONT)
            {
                AppendContinuation(line, stack, last, issues);
                continue;
            }

            var structure = new GedcomStructure(line.Tag) { LineNumber = line.LineNumber };
            SetPayload(structure, line.RawValue);

            if (line.Level == 0)
            {
                if (line.Xref is not null)
                {
                    structure.Xref = line.Xref;
                }

                switch (line.Tag)
                {
                    case GedcomTags.HEAD when header is null:
                        header = structure;
                        break;
                    case GedcomTags.HEAD:
                        issues.Warning(GedcomIssueCodes.DUPLICATE_HEADER, "A second HEAD was ignored.", line.LineNumber);
                        break;
                    case GedcomTags.TRLR:
                        trailer = true;
                        break;
                    default:
                        records.Add(structure);
                        break;
                }

                stack.Clear();
                stack.Add(structure);
                last = structure;
                continue;
            }

            if (stack.Count == 0)
            {
                issues.Error(GedcomIssueCodes.MALFORMED_LINE, $"Level {line.Level} line comes before the first record and was skipped.", line.LineNumber);
                continue;
            }

            int parentDepth = line.Level - 1;
            if (parentDepth >= stack.Count)
            {
                issues.Warning(GedcomIssueCodes.LEVEL_JUMP, $"Level {line.Level} follows level {stack.Count - 1}; {line.Tag} was placed under the previous line.", line.LineNumber);
                parentDepth = stack.Count - 1;
            }

            var parent = stack[parentDepth];
            stack.RemoveRange(parentDepth + 1, stack.Count - parentDepth - 1);
            parent.Children.Add(structure);
            stack.Add(structure);
            last = structure;

            if (line.Xref is not null)
            {
                issues.Warning(GedcomIssueCodes.SUBSTRUCTURE_XREF, $"{line.Tag} is not a record but has the cross-reference id @{line.Xref}@; it was kept.", line.LineNumber);
                structure.Xref = line.Xref;
            }
        }

        if (afterTrailer > 0)
        {
            issues.Warning(GedcomIssueCodes.DATA_AFTER_TRAILER, $"{afterTrailer} line(s) after TRLR were ignored.", firstAfterTrailer);
        }

        return new BuildResult(header, records, trailer);
    }

    private static void AppendContinuation(GedcomLine line, List<GedcomStructure> stack, GedcomStructure? last, GedcomIssueCollector issues)
    {
        if (last is null || stack.Count == 0)
        {
            issues.Error(GedcomIssueCodes.MISPLACED_CONTINUATION, $"{line.Tag} comes before any record and was skipped.", line.LineNumber);
            return;
        }

        int parentDepth = line.Level - 1;
        GedcomStructure target;
        if (parentDepth >= 0 && parentDepth < stack.Count)
        {
            target = stack[parentDepth];
            if (!ReferenceEquals(target, last)
                && line.Level == last.Level
                && !last.HasPointer
                && (target.Value is null || target.HasPointer))
            {
                // "1 NOTE text / 1 CONC more": the continuation was written at the note's own level.
                issues.Warning(GedcomIssueCodes.MISPLACED_CONTINUATION, $"{line.Tag} is at the same level as the line it continues.", line.LineNumber);
                target = last;
            }
        }
        else
        {
            issues.Warning(GedcomIssueCodes.MISPLACED_CONTINUATION, $"{line.Tag} at level {line.Level} does not follow a line at level {line.Level - 1}; it was joined to the previous line.", line.LineNumber);
            target = last;
        }

        if (target.HasPointer)
        {
            issues.Warning(GedcomIssueCodes.MISPLACED_CONTINUATION, $"{line.Tag} continues a pointer and was ignored.", line.LineNumber);
            return;
        }

        string piece = line.RawValue ?? string.Empty;
        target.Value = line.Tag == GedcomTags.CONT
            ? (target.Value ?? string.Empty) + "\n" + piece
            : (target.Value ?? string.Empty) + piece;
    }

    private static void SetPayload(GedcomStructure structure, string? raw)
    {
        if (raw is null)
        {
            return;
        }

        if (IsPointer(raw))
        {
            structure.Pointer = raw[1..^1];
        }
        else
        {
            structure.Value = raw;
        }
    }

    /// <summary>A whole line value of the form <c>@xref@</c>; <c>@#...@</c> escapes and doubled <c>@@</c> are text.</summary>
    internal static bool IsPointer(string raw) =>
        raw.Length >= 3
        && raw[0] == '@'
        && raw[^1] == '@'
        && raw[1] is not '@' and not '#'
        && raw.IndexOf('@', 1) == raw.Length - 1;
}
