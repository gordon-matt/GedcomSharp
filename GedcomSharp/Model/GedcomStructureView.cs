using System.Runtime.CompilerServices;

namespace GedcomSharp.Model;

/// <summary>
/// Base for the typed views over <see cref="GedcomStructure"/>. A view holds no data of its own: reading a
/// property reads the tree and setting one edits it, so typed and untyped access never disagree and tags the
/// view does not model are kept. Two views are equal when they wrap the same structure.
/// </summary>
public abstract class GedcomStructureView : IEquatable<GedcomStructureView>
{
    /// <summary>
    /// Initializes a new instance of the GedcomStructureView class.
    /// </summary>
    /// <param name="structure">The structure.</param>
    protected GedcomStructureView(GedcomStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        Structure = structure;
    }

    /// <summary>
    /// Gets the structure.
    /// </summary>
    public GedcomStructure Structure { get; }

    /// <summary>
    /// Gets the tag.
    /// </summary>
    public string Tag => Structure.Tag;

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value
    {
        get => Structure.Value;
        set => Structure.Value = value;
    }

    /// <summary>The version of the file this structure belongs to, or 7.0 when it is not in a file yet.</summary>
    protected GedcomVersion Version => Structure.File?.Version ?? GedcomVersion.V70;

    /// <summary>
    /// Gets the file.
    /// </summary>
    protected GedcomFile? File => Structure.File;

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The string.</returns>
    protected string? Get(string tag) => Structure.ChildValue(tag);

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="value">The value.</param>
    protected void Set(string tag, string? value) => Structure.SetChildValue(tag, value);

    /// <summary>The value of the first of several alternative tags, e.g. a standard tag and its vendor extension.</summary>
    protected string? GetFirst(params ReadOnlySpan<string> tags) => Structure.Child(tags)?.Value;

    /// <summary>
    /// Gets the values.
    /// </summary>
    /// <param name="tags">The tags.</param>
    /// <returns>The values.</returns>
    protected IEnumerable<string> Values(params string[] tags) =>
        Structure.Children.Where(child => tags.Contains(child.Tag) && child.Value is not null).Select(child => child.Value!);

    /// <summary>
    /// Gets the views.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="tag">The tag.</param>
    /// <param name="create">The create.</param>
    /// <returns>The views.</returns>
    protected IEnumerable<T> Views<T>(string tag, Func<GedcomStructure, T> create) => Structure.ChildrenWithTag(tag).Select(create);

    /// <summary>
    /// Gets the views.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="tags">The tags.</param>
    /// <param name="create">The create.</param>
    /// <returns>The views.</returns>
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

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>true when the condition is met; otherwise, false.</returns>
    public bool Equals(GedcomStructureView? other) => other is not null && ReferenceEquals(Structure, other.Structure);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GedcomStructureView other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Structure);

    /// <inheritdoc/>
    public override string ToString() => Structure.ToString();
}

/// <summary>A view whose structure can carry notes, source citations and multimedia links.</summary>
public abstract class GedcomCitableView(GedcomStructure structure) : GedcomStructureView(structure)
{
    /// <summary>Inline notes and links to shared notes (5.x <c>NOTE @N1@</c>, 7.0 <c>SNOTE @N1@</c>).</summary>
    public IEnumerable<NoteLink> Notes => Views([GedcomTags.NOTE, GedcomTags.SNOTE], child => new NoteLink(child));

    /// <summary>
    /// Gets the source citations.
    /// </summary>
    public IEnumerable<SourceCitation> SourceCitations => Views(GedcomTags.SOUR, child => new SourceCitation(child));

    /// <summary>
    /// Gets the media links.
    /// </summary>
    public IEnumerable<MultimediaLink> MediaLinks => Views(GedcomTags.OBJE, child => new MultimediaLink(child));

    /// <summary>
    /// Adds the note.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The note.</returns>
    public NoteLink AddNote(string text) => new(Structure.AddChild(GedcomTags.NOTE, text));

    /// <summary>Links a shared note record, using SNOTE for 7.0 and NOTE for 5.x.</summary>
    public NoteLink AddNoteReference(string noteXref) =>
        new(Structure.AddPointer(Version.IsSeven ? GedcomTags.SNOTE : GedcomTags.NOTE, noteXref));

    /// <summary>
    /// Adds the source citation.
    /// </summary>
    /// <param name="sourceXref">The source xref.</param>
    /// <param name="page">The page.</param>
    /// <returns>The source citation.</returns>
    public SourceCitation AddSourceCitation(string sourceXref, string? page = null)
    {
        var citation = new SourceCitation(Structure.AddPointer(GedcomTags.SOUR, sourceXref));
        citation.Page = page;
        return citation;
    }

    /// <summary>
    /// Adds the media link.
    /// </summary>
    /// <param name="multimediaXref">The multimedia xref.</param>
    /// <returns>The media link.</returns>
    public MultimediaLink AddMediaLink(string multimediaXref) => new(Structure.AddPointer(GedcomTags.OBJE, multimediaXref));
}
