using GedcomSharp.Conversion;
using GedcomSharp.Text;

namespace GedcomSharp;

/// <summary>How a file was stored on disk, as found by <see cref="GedcomReader"/>.</summary>
/// <param name="FileName">The path or GEDZIP entry the data came from, when known.</param>
/// <param name="DeclaredCharacterSet">The <c>HEAD.CHAR</c> value, which may disagree with the detected encoding.</param>
public sealed record GedcomSourceInfo(
    string? FileName,
    GedcomCharacterEncoding Encoding,
    string EncodingName,
    bool HasByteOrderMark,
    string LineTerminator,
    string? DeclaredCharacterSet,
    string? DeclaredVersion,
    int LineCount);

/// <summary>
/// A GEDCOM dataset: the header, the records in file order, and what was found while reading it.
/// Records are kept as <see cref="GedcomStructure"/> trees so nothing is lost; the typed collections
/// (<see cref="Individuals"/>, <see cref="Families"/>, ...) are views over those trees.
/// </summary>
public sealed class GedcomFile
{
    private Dictionary<string, GedcomStructure>? xrefIndex;
    private readonly Dictionary<string, int> nextXrefNumbers = new(StringComparer.Ordinal);

    /// <summary>Creates an empty dataset with a bare HEAD. Use <see cref="Create"/> for a header filled in for the version.</summary>
    public GedcomFile(GedcomVersion version = GedcomVersion.V70)
        : this(version, new GedcomStructure(GedcomTags.HEAD))
    {
    }

    internal GedcomFile(GedcomVersion version, GedcomStructure header)
    {
        if (header.Tag != GedcomTags.HEAD)
        {
            throw new ArgumentException("The header must be a HEAD structure.", nameof(header));
        }

        Version = version;
        header.Owner = this;
        Header = new GedcomHeader(header);
        Records = new GedcomRecordCollection(this);
    }

    /// <summary>Creates a dataset whose header declares <paramref name="version"/>, ready to add records to.</summary>
    public static GedcomFile Create(GedcomVersion version = GedcomVersion.V70, string? sourceSystem = null)
    {
        if (version == GedcomVersion.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Choose a GEDCOM version.");
        }

        var file = new GedcomFile(version);
        var header = file.HeaderStructure;
        header.AddChild(GedcomTags.SOUR, string.IsNullOrWhiteSpace(sourceSystem) ? "GedcomSharp" : sourceSystem);
        var gedc = header.AddChild(GedcomTags.GEDC);
        gedc.AddChild(GedcomTags.VERS, version.ToVersionString());
        if (version.IsLegacy)
        {
            var form = gedc.AddChild(GedcomTags.FORM, "LINEAGE-LINKED");
            if (version == GedcomVersion.V555)
            {
                form.AddChild(GedcomTags.VERS, "5.5.5");
            }

            header.AddChild(GedcomTags.CHAR, "UTF-8");
        }

        return file;
    }

    /// <summary>The version the data follows. Changing it does not convert anything; use <see cref="ConvertTo"/> for that.</summary>
    public GedcomVersion Version { get; set; }

    public GedcomHeader Header { get; }

    public GedcomStructure HeaderStructure => Header.Structure;

    /// <summary>All records except HEAD and TRLR, in file order.</summary>
    public GedcomRecordCollection Records { get; }

    /// <summary>Problems found while reading or converting. Empty for a dataset built in code.</summary>
    public IReadOnlyList<GedcomIssue> Issues { get; internal set; } = [];

    /// <summary>How the data was stored, when it was read from a file.</summary>
    public GedcomSourceInfo? Source { get; internal set; }

    public IEnumerable<IndividualRecord> Individuals => Typed(GedcomTags.INDI, structure => new IndividualRecord(structure));

    public IEnumerable<FamilyRecord> Families => Typed(GedcomTags.FAM, structure => new FamilyRecord(structure));

    public IEnumerable<SourceRecord> Sources => Typed(GedcomTags.SOUR, structure => new SourceRecord(structure));

    public IEnumerable<RepositoryRecord> Repositories => Typed(GedcomTags.REPO, structure => new RepositoryRecord(structure));

    /// <summary>Shared notes: 5.x NOTE records and 7.0 SNOTE records.</summary>
    public IEnumerable<NoteRecord> Notes => Records
        .Where(record => record.Tag is GedcomTags.NOTE or GedcomTags.SNOTE)
        .Select(record => new NoteRecord(record));

    public IEnumerable<MultimediaRecord> Multimedia => Typed(GedcomTags.OBJE, structure => new MultimediaRecord(structure));

    public IEnumerable<SubmitterRecord> Submitters => Typed(GedcomTags.SUBM, structure => new SubmitterRecord(structure));

    public IEnumerable<SubmissionRecord> Submissions => Typed(GedcomTags.SUBN, structure => new SubmissionRecord(structure));

    /// <summary>Every record wrapped in its typed view.</summary>
    public IEnumerable<GedcomRecord> AllRecords => Records.Select(GedcomRecord.Create);

    /// <summary>The record with <paramref name="xref"/> (with or without @ signs). The first wins when an xref is duplicated.</summary>
    public GedcomStructure? Find(string? xref)
    {
        string? key = GedcomStructure.NormalizeXref(xref);
        if (key is null)
        {
            return null;
        }

        xrefIndex ??= BuildIndex();
        return xrefIndex.GetValueOrDefault(key);
    }

    public T? Find<T>(string? xref) where T : GedcomRecord => Find(xref) is { } structure ? GedcomRecord.Create(structure) as T : null;

    public GedcomRecord? FindRecord(string? xref) => Find(xref) is { } structure ? GedcomRecord.Create(structure) : null;

    public bool Contains(string? xref) => Find(xref) is not null;

    /// <summary>All structures in the dataset that point to <paramref name="xref"/>.</summary>
    public IEnumerable<GedcomStructure> FindReferences(string xref)
    {
        string? key = GedcomStructure.NormalizeXref(xref);
        if (key is null)
        {
            yield break;
        }

        foreach (var structure in HeaderStructure.Descendants())
        {
            if (structure.Pointer == key)
            {
                yield return structure;
            }
        }

        foreach (var record in Records)
        {
            foreach (var structure in record.Descendants())
            {
                if (structure.Pointer == key)
                {
                    yield return structure;
                }
            }
        }
    }

    /// <summary>An xref that is not in use: <paramref name="prefix"/> followed by a number (<c>I1</c>, <c>I2</c>, ...).</summary>
    public string CreateXref(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        int number = nextXrefNumbers.GetValueOrDefault(prefix, 1);
        while (Contains(prefix + number.ToString(CultureInfo.InvariantCulture)))
        {
            number++;
        }

        nextXrefNumbers[prefix] = number + 1;
        return prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Adds a record structure, giving it an xref when it has none.</summary>
    public GedcomRecord Add(GedcomStructure record)
    {
        record.Xref ??= CreateXref(DefaultPrefix(record.Tag));
        Records.Add(record);
        return GedcomRecord.Create(record);
    }

    public IndividualRecord AddIndividual(string? xref = null) => AddTyped(GedcomTags.INDI, xref, structure => new IndividualRecord(structure));

    public FamilyRecord AddFamily(string? xref = null) => AddTyped(GedcomTags.FAM, xref, structure => new FamilyRecord(structure));

    public SourceRecord AddSource(string? title = null, string? xref = null)
    {
        var source = AddTyped(GedcomTags.SOUR, xref, structure => new SourceRecord(structure));
        source.Title = title;
        return source;
    }

    public RepositoryRecord AddRepository(string name, string? xref = null)
    {
        var repository = AddTyped(GedcomTags.REPO, xref, structure => new RepositoryRecord(structure));
        repository.Name = name;
        return repository;
    }

    /// <summary>Adds a shared note: SNOTE in 7.0, NOTE in 5.x.</summary>
    public NoteRecord AddNote(string text, string? xref = null)
    {
        var note = AddTyped(Version.IsSeven ? GedcomTags.SNOTE : GedcomTags.NOTE, xref, structure => new NoteRecord(structure));
        note.Text = text;
        return note;
    }

    public MultimediaRecord AddMultimedia(string? path = null, string? xref = null)
    {
        var multimedia = AddTyped(GedcomTags.OBJE, xref, structure => new MultimediaRecord(structure));
        if (path is not null)
        {
            multimedia.AddFile(path);
        }

        return multimedia;
    }

    public SubmitterRecord AddSubmitter(string name, string? xref = null)
    {
        var submitter = AddTyped(GedcomTags.SUBM, xref, structure => new SubmitterRecord(structure));
        submitter.Name = name;
        return submitter;
    }

    /// <summary>Links two partners and their family both ways (FAM.HUSB/WIFE and INDI.FAMS).</summary>
    public FamilyRecord AddFamily(IndividualRecord? husband, IndividualRecord? wife, params IEnumerable<IndividualRecord> children)
    {
        var family = AddFamily();
        if (husband is not null)
        {
            family.HusbandXref = husband.Xref;
            husband.AddSpouseToFamilyLink(family.Xref!);
        }

        if (wife is not null)
        {
            family.WifeXref = wife.Xref;
            wife.AddSpouseToFamilyLink(family.Xref!);
        }

        foreach (var child in children)
        {
            AddChildToFamily(family, child);
        }

        return family;
    }

    /// <summary>Adds CHIL to the family and FAMC to the child.</summary>
    public void AddChildToFamily(FamilyRecord family, IndividualRecord child, string? pedigree = null)
    {
        family.AddChild(child.Xref!);
        child.AddChildToFamilyLink(family.Xref!, pedigree);
    }

    /// <summary>Removes a record. With <paramref name="removeReferences"/>, structures pointing to it (FAMS, CHIL, SOUR citations...) are removed too.</summary>
    public bool Remove(GedcomRecord record, bool removeReferences = true) => Remove(record.Structure, removeReferences);

    public bool Remove(GedcomStructure record, bool removeReferences = true)
    {
        if (!Records.Remove(record))
        {
            return false;
        }

        if (removeReferences && record.Xref is string xref && !Contains(xref))
        {
            foreach (var reference in FindReferences(xref).ToList())
            {
                reference.Remove();
            }
        }

        return true;
    }

    /// <summary>A deep copy, detached from this dataset.</summary>
    public GedcomFile Clone()
    {
        var copy = new GedcomFile(Version, HeaderStructure.Clone())
        {
            Issues = Issues,
            Source = Source
        };

        foreach (var record in Records)
        {
            copy.Records.Add(record.Clone());
        }

        return copy;
    }

    /// <summary>
    /// A copy of the dataset converted to <paramref name="version"/>, with <see cref="Issues"/> listing what
    /// could not be carried over exactly. This dataset is not changed.
    /// </summary>
    public GedcomFile ConvertTo(GedcomVersion version) => GedcomVersionConverter.Convert(this, version);

    internal void OnXrefChanged() => xrefIndex = null;

    internal static string DefaultPrefix(string tag) => tag switch
    {
        GedcomTags.INDI => "I",
        GedcomTags.FAM => "F",
        GedcomTags.SOUR => "S",
        GedcomTags.REPO => "R",
        GedcomTags.NOTE or GedcomTags.SNOTE => "N",
        GedcomTags.OBJE => "O",
        GedcomTags.SUBM => "U",
        GedcomTags.SUBN => "SUBN",
        _ => "X"
    };

    private T AddTyped<T>(string tag, string? xref, Func<GedcomStructure, T> create)
    {
        var structure = new GedcomStructure(tag) { Xref = xref };
        structure.Xref ??= CreateXref(DefaultPrefix(tag));
        if (Contains(structure.Xref))
        {
            throw new InvalidOperationException($"@{structure.Xref}@ is already in use.");
        }

        Records.Add(structure);
        return create(structure);
    }

    private IEnumerable<T> Typed<T>(string tag, Func<GedcomStructure, T> create) =>
        Records.Where(record => record.Tag == tag).Select(create);

    private Dictionary<string, GedcomStructure> BuildIndex()
    {
        var index = new Dictionary<string, GedcomStructure>(Records.Count, StringComparer.Ordinal);
        foreach (var record in Records)
        {
            if (record.Xref is string xref)
            {
                index.TryAdd(xref, record);
            }
        }

        return index;
    }
}
