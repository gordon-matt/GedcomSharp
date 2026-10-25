namespace GedcomSharp.Parsing;

/// <summary>One physical line after tokenizing, before payload escapes are undone.</summary>
/// <param name="Level">The level number, or -1 when the line has none (a broken continuation line).</param>
/// <param name="RawValue">Everything after the delimiter that follows the tag, as written.</param>
internal readonly record struct GedcomLine(int LineNumber, int Level, string? Xref, string Tag, string? RawValue, string RawText)
{
    public bool HasLevel => Level >= 0;
}
