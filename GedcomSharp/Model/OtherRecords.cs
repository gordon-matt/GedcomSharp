namespace GedcomSharp.Model;

/// <summary><c>SOUR</c>: a source of information, such as a register, census or book.</summary>
public sealed class SourceRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    public string? Title
    {
        get => Get(GedcomTags.TITL);
        set => Set(GedcomTags.TITL, value);
    }

    public string? Author
    {
        get => Get(GedcomTags.AUTH);
        set => Set(GedcomTags.AUTH, value);
    }

    public string? Abbreviation
    {
        get => Get(GedcomTags.ABBR);
        set => Set(GedcomTags.ABBR, value);
    }

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

    public string? Agency
    {
        get => Structure.Child(GedcomTags.DATA)?.ChildValue(GedcomTags.AGNC);
        set => Structure.GetOrAddChild(GedcomTags.DATA).SetChildValue(GedcomTags.AGNC, value);
    }

    /// <summary>The kinds of event the source records (<c>DATA.EVEN</c>, comma-separated tags such as "BIRT, DEAT").</summary>
    public IEnumerable<GedcomStructure> RecordedEvents => Structure.Child(GedcomTags.DATA)?.ChildrenWithTag(GedcomTags.EVEN) ?? [];

    public IEnumerable<RepositoryCitation> RepositoryCitations => Views(GedcomTags.REPO, child => new RepositoryCitation(child));

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
    public string? Name
    {
        get => Get(GedcomTags.NAME);
        set => Set(GedcomTags.NAME, value);
    }

    public ContactDetails Contact => new(Structure);
}

/// <summary>A shared note: 5.x <c>NOTE</c> record or 7.0 <c>SNOTE</c> record. The text is the record's value.</summary>
public sealed class NoteRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    public string? Text
    {
        get => Value;
        set => Value = value;
    }

    public string? MediaType
    {
        get => GetFirst(GedcomTags.MIME, "_MIME");
        set => Set(GedcomTags.MIME, value);
    }

    public string? Language
    {
        get => Get(GedcomTags.LANG);
        set => Set(GedcomTags.LANG, value);
    }

    public IEnumerable<GedcomStructure> Translations => Structure.ChildrenWithTag(GedcomTags.TRAN);
}

/// <summary><c>OBJE</c>: a multimedia record with one or more files.</summary>
public sealed class MultimediaRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    public IEnumerable<MultimediaFile> Files => Views(GedcomTags.FILE, child => new MultimediaFile(child));

    public MultimediaFile? PrimaryFile => Files.FirstOrDefault();

    public string? Title => PrimaryFile?.Title ?? Get(GedcomTags.TITL);

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
    public string? Name
    {
        get => Get(GedcomTags.NAME);
        set => Set(GedcomTags.NAME, value);
    }

    public ContactDetails Contact => new(Structure);

    public IEnumerable<string> Languages => Values(GedcomTags.LANG);
}

/// <summary><c>SUBN</c> (5.x only): a submission to an LDS processing centre. Removed in 7.0.</summary>
public sealed class SubmissionRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    public string? SubmitterXref
    {
        get => Structure.Child(GedcomTags.SUBM)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.SUBM, value);
    }

    public string? FamilyFile
    {
        get => Get(GedcomTags.FAMF);
        set => Set(GedcomTags.FAMF, value);
    }

    public string? Temple
    {
        get => Get(GedcomTags.TEMP);
        set => Set(GedcomTags.TEMP, value);
    }
}
