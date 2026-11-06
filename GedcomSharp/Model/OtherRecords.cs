namespace GedcomSharp.Model;

/// <summary><c>SOUR</c>: a source of information, such as a register, census or book.</summary>
public sealed class SourceRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string? Title
    {
        get => Get(GedcomTags.TITL);
        set => Set(GedcomTags.TITL, value);
    }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string? Author
    {
        get => Get(GedcomTags.AUTH);
        set => Set(GedcomTags.AUTH, value);
    }

    /// <summary>
    /// Gets or sets the abbreviation.
    /// </summary>
    public string? Abbreviation
    {
        get => Get(GedcomTags.ABBR);
        set => Set(GedcomTags.ABBR, value);
    }

    /// <summary>
    /// Gets or sets the publication.
    /// </summary>
    public string? Publication
    {
        get => Get(GedcomTags.PUBL);
        set => Set(GedcomTags.PUBL, value);
    }

    /// <summary>Verbatim text from the source (<c>TEXT</c>).</summary>
    public string? Text
    {
        get => Get(GedcomTags.TEXT);
        set => Set(GedcomTags.TEXT, value);
    }

    /// <summary>
    /// Gets or sets the agency.
    /// </summary>
    public string? Agency
    {
        get => Structure.Child(GedcomTags.DATA)?.ChildValue(GedcomTags.AGNC);
        set => Structure.GetOrAddChild(GedcomTags.DATA).SetChildValue(GedcomTags.AGNC, value);
    }

    /// <summary>The kinds of event the source records (<c>DATA.EVEN</c>, comma-separated tags such as "BIRT, DEAT").</summary>
    public IEnumerable<GedcomStructure> RecordedEvents => Structure.Child(GedcomTags.DATA)?.ChildrenWithTag(GedcomTags.EVEN) ?? [];

    /// <summary>
    /// Gets the repository citations.
    /// </summary>
    public IEnumerable<RepositoryCitation> RepositoryCitations => Views(GedcomTags.REPO, child => new RepositoryCitation(child));

    /// <summary>
    /// Adds the repository citation.
    /// </summary>
    /// <param name="repositoryXref">The repository xref.</param>
    /// <param name="callNumber">The call number.</param>
    /// <returns>The repository citation.</returns>
    public RepositoryCitation AddRepositoryCitation(string repositoryXref, string? callNumber = null)
    {
        var citation = Structure.AddPointer(GedcomTags.REPO, repositoryXref);
        if (callNumber is not null)
        {
            citation.AddChild(GedcomTags.CALN, callNumber);
        }

        return new RepositoryCitation(citation);
    }
}

/// <summary><c>REPO</c>: an archive, library or other holder of sources.</summary>
public sealed class RepositoryRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name
    {
        get => Get(GedcomTags.NAME);
        set => Set(GedcomTags.NAME, value);
    }

    /// <summary>
    /// Gets the contact.
    /// </summary>
    public ContactDetails Contact => new(Structure);
}

/// <summary>A shared note: 5.x <c>NOTE</c> record or 7.0 <c>SNOTE</c> record. The text is the record's value.</summary>
public sealed class NoteRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string? Text
    {
        get => Value;
        set => Value = value;
    }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string? MediaType
    {
        get => GetFirst(GedcomTags.MIME, "_MIME");
        set => Set(GedcomTags.MIME, value);
    }

    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    public string? Language
    {
        get => Get(GedcomTags.LANG);
        set => Set(GedcomTags.LANG, value);
    }

    /// <summary>
    /// Gets the translations.
    /// </summary>
    public IEnumerable<GedcomStructure> Translations => Structure.ChildrenWithTag(GedcomTags.TRAN);
}

/// <summary><c>OBJE</c>: a multimedia record with one or more files.</summary>
public sealed class MultimediaRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets the files.
    /// </summary>
    public IEnumerable<MultimediaFile> Files => Views(GedcomTags.FILE, child => new MultimediaFile(child));

    /// <summary>
    /// Gets the primary file.
    /// </summary>
    public MultimediaFile? PrimaryFile => Files.FirstOrDefault();

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string? Title => PrimaryFile?.Title ?? Get(GedcomTags.TITL);

    /// <summary>
    /// Adds the file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="format">The format.</param>
    /// <param name="title">The title.</param>
    /// <returns>The file.</returns>
    public MultimediaFile AddFile(string path, string? format = null, string? title = null)
    {
        var file = new MultimediaFile(Structure.AddChild(GedcomTags.FILE, path));
        file.Format = format ?? (Version.IsSeven ? MediaTypes.FromPath(path) : MediaTypes.ToLegacyFormat(MediaTypes.FromPath(path))) ?? file.Format;
        if (title is not null)
        {
            file.Title = title;
        }

        return file;
    }
}

/// <summary><c>SUBM</c>: the person or organisation that contributed the data.</summary>
public sealed class SubmitterRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name
    {
        get => Get(GedcomTags.NAME);
        set => Set(GedcomTags.NAME, value);
    }

    /// <summary>
    /// Gets the contact.
    /// </summary>
    public ContactDetails Contact => new(Structure);

    /// <summary>
    /// Gets the languages.
    /// </summary>
    public IEnumerable<string> Languages => Values(GedcomTags.LANG);
}

/// <summary><c>SUBN</c> (5.x only): a submission to an LDS processing centre. Removed in 7.0.</summary>
public sealed class SubmissionRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets or sets the submitter xref.
    /// </summary>
    public string? SubmitterXref
    {
        get => Structure.Child(GedcomTags.SUBM)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.SUBM, value);
    }

    /// <summary>
    /// Gets or sets the family file.
    /// </summary>
    public string? FamilyFile
    {
        get => Get(GedcomTags.FAMF);
        set => Set(GedcomTags.FAMF, value);
    }

    /// <summary>
    /// Gets or sets the temple.
    /// </summary>
    public string? Temple
    {
        get => Get(GedcomTags.TEMP);
        set => Set(GedcomTags.TEMP, value);
    }
}
