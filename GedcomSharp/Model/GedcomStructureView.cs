using System.Runtime.CompilerServices;

namespace GedcomSharp.Model;

/// <summary>
/// Base for the typed views over <see cref="GedcomStructure"/>. A view holds no data of its own: reading a
/// property reads the tree and setting one edits it, so typed and untyped access never disagree and tags the
/// view does not model are kept. Two views are equal when they wrap the same structure.
/// </summary>
public abstract class GedcomStructureView : IEquatable<GedcomStructureView>
{
    protected GedcomStructureView(GedcomStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        Structure = structure;
    }

    public GedcomStructure Structure { get; }

    public string Tag => Structure.Tag;

    public string? Value
    {
        get => Structure.Value;
        set => Structure.Value = value;
    }

    /// <summary>The version of the file this structure belongs to, or 7.0 when it is not in a file yet.</summary>
    protected GedcomVersion Version => Structure.File?.Version ?? GedcomVersion.V70;

    protected GedcomFile? File => Structure.File;

    protected string? Get(string tag) => Structure.ChildValue(tag);

    protected void Set(string tag, string? value) => Structure.SetChildValue(tag, value);

    /// <summary>The value of the first of several alternative tags, e.g. a standard tag and its vendor extension.</summary>
    protected string? GetFirst(params ReadOnlySpan<string> tags) => Structure.Child(tags)?.Value;

    protected IEnumerable<string> Values(params string[] tags) =>
        Structure.Children.Where(child => tags.Contains(child.Tag) && child.Value is not null).Select(child => child.Value!);

    protected IEnumerable<T> Views<T>(string tag, Func<GedcomStructure, T> create) => Structure.ChildrenWithTag(tag).Select(create);

    protected IEnumerable<T> Views<T>(string[] tags, Func<GedcomStructure, T> create) => Structure.ChildrenWithTag(tags).Select(create);

    /// <summary>Reads a DATE child together with its 7.0 PHRASE.</summary>
    protected GedcomDate GetDate(string tag = GedcomTags.DATE)
    {
        var date = Structure.Child(tag);
        return date is null ? GedcomDate.Empty : GedcomDate.Parse(date.Value, date.ChildValue(GedcomTags.PHRASE));
    }

    /// <summary>Writes a DATE child in the file's syntax, adding or removing its PHRASE as 7.0 requires.</summary>
    protected void SetDate(GedcomDate? date, string tag = GedcomTags.DATE)
    {
        if (date is null || date.IsEmpty)
        {
            Structure.RemoveChildren(tag);
            return;
        }

        var (value, phrase) = date.FormatWithPhrase(Version);
        var child = Structure.GetOrAddChild(tag);
        child.Value = value.Length == 0 ? null : value;
        if (Version.IsSeven)
        {
            child.SetChildValue(GedcomTags.PHRASE, phrase);
        }
    }

    public bool Equals(GedcomStructureView? other) => other is not null && ReferenceEquals(Structure, other.Structure);

    public override bool Equals(object? obj) => obj is GedcomStructureView other && Equals(other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Structure);

    public override string ToString() => Structure.ToString();
}

/// <summary>A view whose structure can carry notes, source citations and multimedia links.</summary>
public abstract class GedcomCitableView(GedcomStructure structure) : GedcomStructureView(structure)
{
    /// <summary>Inline notes and links to shared notes (5.x <c>NOTE @N1@</c>, 7.0 <c>SNOTE @N1@</c>).</summary>
    public IEnumerable<NoteLink> Notes => Views([GedcomTags.NOTE, GedcomTags.SNOTE], child => new NoteLink(child));

    public IEnumerable<SourceCitation> SourceCitations => Views(GedcomTags.SOUR, child => new SourceCitation(child));

    public IEnumerable<MultimediaLink> MediaLinks => Views(GedcomTags.OBJE, child => new MultimediaLink(child));

    public NoteLink AddNote(string text) => new(Structure.AddChild(GedcomTags.NOTE, text));

    /// <summary>Links a shared note record, using SNOTE for 7.0 and NOTE for 5.x.</summary>
    public NoteLink AddNoteReference(string noteXref) =>
        new(Structure.AddPointer(Version.IsSeven ? GedcomTags.SNOTE : GedcomTags.NOTE, noteXref));

    public SourceCitation AddSourceCitation(string sourceXref, string? page = null)
    {
        var citation = new SourceCitation(Structure.AddPointer(GedcomTags.SOUR, sourceXref));
        citation.Page = page;
        return citation;
    }

    public MultimediaLink AddMediaLink(string multimediaXref) => new(Structure.AddPointer(GedcomTags.OBJE, multimediaXref));
}
