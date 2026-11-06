namespace GedcomSharp;

/// <summary>
/// Specifies the gedcom issue severity.
/// </summary>
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
    /// <inheritdoc/>
    public override string ToString() => LineNumber is int line
        ? $"{Severity} {Code} (line {line}): {Message}"
        : $"{Severity} {Code}: {Message}";
}

/// <summary>
/// Provides gedcom issue codes.
/// </summary>
public static class GedcomIssueCodes
{
    /// <summary>
    /// The empty file constant.
    /// </summary>
    public const string EMPTY_FILE = "EmptyFile";
    /// <summary>
    /// The not gedcom constant.
    /// </summary>
    public const string NOT_GEDCOM = "NotGedcom";
    /// <summary>
    /// The ftw text constant.
    /// </summary>
    public const string FTW_TEXT = "FtwText";
    /// <summary>
    /// The encoding constant.
    /// </summary>
    public const string ENCODING = "Encoding";
    /// <summary>
    /// The encoding mismatch constant.
    /// </summary>
    public const string ENCODING_MISMATCH = "EncodingMismatch";
    /// <summary>
    /// The invalid characters constant.
    /// </summary>
    public const string INVALID_CHARACTERS = "InvalidCharacters";
    /// <summary>
    /// The repaired utf8 constant.
    /// </summary>
    public const string REPAIRED_UTF8 = "RepairedUtf8";
    /// <summary>
    /// The blank line constant.
    /// </summary>
    public const string BLANK_LINE = "BlankLine";
    /// <summary>
    /// The leading whitespace constant.
    /// </summary>
    public const string LEADING_WHITESPACE = "LeadingWhitespace";
    /// <summary>
    /// The mixed line terminators constant.
    /// </summary>
    public const string MIXED_LINE_TERMINATORS = "MixedLineTerminators";
    /// <summary>
    /// The malformed line constant.
    /// </summary>
    public const string MALFORMED_LINE = "MalformedLine";
    /// <summary>
    /// The missing level constant.
    /// </summary>
    public const string MISSING_LEVEL = "MissingLevel";
    /// <summary>
    /// The level jump constant.
    /// </summary>
    public const string LEVEL_JUMP = "LevelJump";
    /// <summary>
    /// The extra delimiters constant.
    /// </summary>
    public const string EXTRA_DELIMITERS = "ExtraDelimiters";
    /// <summary>
    /// The invalid tag constant.
    /// </summary>
    public const string INVALID_TAG = "InvalidTag";
    /// <summary>
    /// The tag case constant.
    /// </summary>
    public const string TAG_CASE = "TagCase";
    /// <summary>
    /// The invalid xref constant.
    /// </summary>
    public const string INVALID_XREF = "InvalidXref";
    /// <summary>
    /// The substructure xref constant.
    /// </summary>
    public const string SUBSTRUCTURE_XREF = "SubstructureXref";
    /// <summary>
    /// The duplicate xref constant.
    /// </summary>
    public const string DUPLICATE_XREF = "DuplicateXref";
    /// <summary>
    /// The misplaced continuation constant.
    /// </summary>
    public const string MISPLACED_CONTINUATION = "MisplacedContinuation";
    /// <summary>
    /// The conc in version 7 constant.
    /// </summary>
    public const string CONC_IN_VERSION_7 = "ConcInVersion7";
    /// <summary>
    /// The unescaped at constant.
    /// </summary>
    public const string UNESCAPED_AT = "UnescapedAt";
    /// <summary>
    /// The missing header constant.
    /// </summary>
    public const string MISSING_HEADER = "MissingHeader";
    /// <summary>
    /// The duplicate header constant.
    /// </summary>
    public const string DUPLICATE_HEADER = "DuplicateHeader";
    /// <summary>
    /// The missing trailer constant.
    /// </summary>
    public const string MISSING_TRAILER = "MissingTrailer";
    /// <summary>
    /// The data after trailer constant.
    /// </summary>
    public const string DATA_AFTER_TRAILER = "DataAfterTrailer";
    /// <summary>
    /// The version constant.
    /// </summary>
    public const string VERSION = "Version";
    /// <summary>
    /// The unresolved pointer constant.
    /// </summary>
    public const string UNRESOLVED_POINTER = "UnresolvedPointer";
    /// <summary>
    /// The conversion constant.
    /// </summary>
    public const string CONVERSION = "Conversion";
    /// <summary>
    /// The unrepresentable characters constant.
    /// </summary>
    public const string UNREPRESENTABLE_CHARACTERS = "UnrepresentableCharacters";
    /// <summary>
    /// The truncated constant.
    /// </summary>
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
