namespace GedcomSharp;

public sealed class GedcomReaderOptions
{
    public static GedcomReaderOptions Default { get; } = new();

    /// <summary>
    /// When true, any <see cref="GedcomIssueSeverity.Error"/> issue (a line that cannot be parsed, a line with no level)
    /// makes the read fail with <see cref="ResultStatus.Invalid"/>. When false (the default) the data that could be
    /// recovered is returned and the problems are listed in <see cref="GedcomFile.Issues"/>.
    /// </summary>
    public bool Strict { get; set; }

    /// <summary>Skips detection and decodes with this encoding. A byte order mark is still skipped.</summary>
    public Encoding? Encoding { get; set; }

    /// <summary>Reads lowercase tags such as <c>Note</c> as uppercase. Tags are case-sensitive in GEDCOM, but some exporters get this wrong.</summary>
    public bool NormalizeTagCase { get; set; } = true;

    /// <summary>
    /// Treats a line that does not start with a level number as a new line of the previous value. Some exporters
    /// write a newline inside a note without CONT; with this off such lines are skipped.
    /// </summary>
    public bool RecoverLinesWithoutLevel { get; set; } = true;

    /// <summary>Reports pointers to records that do not exist in the file.</summary>
    public bool CheckPointers { get; set; } = true;

    /// <summary>The most issues of one kind listed individually; the rest are summarised in one issue.</summary>
    public int MaxIssuesPerKind { get; set; } = 25;
}
