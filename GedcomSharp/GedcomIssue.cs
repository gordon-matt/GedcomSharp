namespace GedcomSharp;

public enum GedcomIssueSeverity
{
    /// <summary>Something was adjusted that does not change the data, such as a blank line that was skipped.</summary>
    Info,

    /// <summary>The file breaks a rule but the data was recovered, such as an undoubled @ or a mislabelled encoding.</summary>
    Warning,

    /// <summary>Data could not be read as intended, such as a line without a tag. Fatal when reading in strict mode.</summary>
    Error
}

/// <summary>A problem found while reading, converting or writing a dataset.</summary>
/// <param name="Code">A stable identifier from <see cref="GedcomIssueCodes"/>, for filtering.</param>
/// <param name="LineNumber">The 1-based source line, when the issue relates to one.</param>
public sealed record GedcomIssue(GedcomIssueSeverity Severity, string Code, string Message, int? LineNumber = null)
{
    public override string ToString() => LineNumber is int line
        ? $"{Severity} {Code} (line {line}): {Message}"
        : $"{Severity} {Code}: {Message}";
}

public static class GedcomIssueCodes
{
    public const string EMPTY_FILE = "EmptyFile";
    public const string NOT_GEDCOM = "NotGedcom";
    public const string FTW_TEXT = "FtwText";
    public const string ENCODING = "Encoding";
    public const string ENCODING_MISMATCH = "EncodingMismatch";
    public const string INVALID_CHARACTERS = "InvalidCharacters";
    public const string REPAIRED_UTF8 = "RepairedUtf8";
    public const string BLANK_LINE = "BlankLine";
    public const string LEADING_WHITESPACE = "LeadingWhitespace";
    public const string MIXED_LINE_TERMINATORS = "MixedLineTerminators";
    public const string MALFORMED_LINE = "MalformedLine";
    public const string MISSING_LEVEL = "MissingLevel";
    public const string LEVEL_JUMP = "LevelJump";
    public const string EXTRA_DELIMITERS = "ExtraDelimiters";
    public const string INVALID_TAG = "InvalidTag";
    public const string TAG_CASE = "TagCase";
    public const string INVALID_XREF = "InvalidXref";
    public const string SUBSTRUCTURE_XREF = "SubstructureXref";
    public const string DUPLICATE_XREF = "DuplicateXref";
    public const string MISPLACED_CONTINUATION = "MisplacedContinuation";
    public const string CONC_IN_VERSION_7 = "ConcInVersion7";
    public const string UNESCAPED_AT = "UnescapedAt";
    public const string MISSING_HEADER = "MissingHeader";
    public const string DUPLICATE_HEADER = "DuplicateHeader";
    public const string MISSING_TRAILER = "MissingTrailer";
    public const string DATA_AFTER_TRAILER = "DataAfterTrailer";
    public const string VERSION = "Version";
    public const string UNRESOLVED_POINTER = "UnresolvedPointer";
    public const string CONVERSION = "Conversion";
    public const string UNREPRESENTABLE_CHARACTERS = "UnrepresentableCharacters";
    public const string TRUNCATED = "IssuesTruncated";
}

/// <summary>
/// Collects issues, folding repeats of the same code into one summary after a limit so a badly broken
/// 100,000-line file does not produce 100,000 warnings.
/// </summary>
internal sealed class GedcomIssueCollector(int maxPerCode = 25, int maxTotal = 2000)
{
    private readonly List<GedcomIssue> issues = [];
    private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GedcomIssueSeverity> severities = new(StringComparer.Ordinal);

    public bool HasErrors { get; private set; }

    public void Info(string code, string message, int? line = null) => Add(GedcomIssueSeverity.Info, code, message, line);

    public void Warning(string code, string message, int? line = null) => Add(GedcomIssueSeverity.Warning, code, message, line);

    public void Error(string code, string message, int? line = null) => Add(GedcomIssueSeverity.Error, code, message, line);

    public void Add(GedcomIssueSeverity severity, string code, string message, int? line = null)
    {
        if (severity == GedcomIssueSeverity.Error)
        {
            HasErrors = true;
        }

        int count = counts.GetValueOrDefault(code) + 1;
        counts[code] = count;
        if (!severities.TryGetValue(code, out var known) || severity > known)
        {
            severities[code] = severity;
        }

        if (count <= maxPerCode && issues.Count < maxTotal)
        {
            issues.Add(new GedcomIssue(severity, code, message, line));
        }
    }

    public void AddRange(IEnumerable<GedcomIssue> source)
    {
        foreach (var issue in source)
        {
            Add(issue.Severity, issue.Code, issue.Message, issue.LineNumber);
        }
    }

    public IReadOnlyList<GedcomIssue> ToList()
    {
        var result = new List<GedcomIssue>(issues);
        foreach (var (code, count) in counts)
        {
            if (count > maxPerCode)
            {
                result.Add(new GedcomIssue(
                    severities[code],
                    code,
                    $"{count - maxPerCode} more {code} issue(s) were not listed individually ({count} in total)."));
            }
        }

        return result;
    }
}
