namespace GedcomSharp.Model;

/// <summary><c>REFN</c>: a user-defined number or code for a record, with an optional <c>TYPE</c>.</summary>
public sealed class UserReference(GedcomStructure structure) : GedcomStructureView(structure)
{
    public string? Number
    {
        get => Value;
        set => Value = value;
    }

    public string? Type
    {
        get => Get(GedcomTags.TYPE);
        set => Set(GedcomTags.TYPE, value);
    }
}

/// <summary><c>EXID</c> (7.0): an identifier assigned by an external authority, scoped by a <c>TYPE</c> URI.</summary>
public sealed class ExternalId(GedcomStructure structure) : GedcomStructureView(structure)
{
    public const string AFN_TYPE = "https://gedcom.io/terms/v7/AFN";
    public const string RFN_TYPE = "https://gedcom.io/terms/v7/RFN";
    public const string RIN_TYPE = "https://gedcom.io/terms/v7/RIN";

    public string? Id
    {
        get => Value;
        set => Value = value;
    }

    public string? Type
    {
        get => Get(GedcomTags.TYPE);
        set => Set(GedcomTags.TYPE, value);
    }
}

/// <summary><c>CHAN</c> or <c>CREA</c>: when a record was last changed or created.</summary>
public sealed class ChangeDate(GedcomStructure structure) : GedcomCitableView(structure)
{
    public GedcomDate Date
    {
        get => GetDate();
        set => SetDate(value);
    }

    /// <summary>The <c>DATE.TIME</c> value as written, e.g. <c>14:05:33</c> or 7.0 <c>14:05:33Z</c>.</summary>
    public string? Time
    {
        get => Structure.Child(GedcomTags.DATE)?.ChildValue(GedcomTags.TIME);
        set => Structure.GetOrAddChild(GedcomTags.DATE).SetChildValue(GedcomTags.TIME, value);
    }

    /// <summary>DATE and TIME combined. A 7.0 time ending in Z is returned as UTC.</summary>
    public DateTime? Timestamp
    {
        get
        {
            if (Date.ToDateOnly() is not DateOnly day)
            {
                return null;
            }

            if (Time is not { Length: > 0 } time)
            {
                return day.ToDateTime(TimeOnly.MinValue);
            }

            bool utc = time.EndsWith('Z') || time.EndsWith('z');
            string text = utc ? time[..^1] : time;
            string[] formats = ["H:mm", "H:mm:ss", "H:mm:ss.FFFFFFF"];
            if (!TimeOnly.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock))
            {
                return day.ToDateTime(TimeOnly.MinValue);
            }

            return day.ToDateTime(clock, utc ? DateTimeKind.Utc : DateTimeKind.Unspecified);
        }

        set
        {
            if (value is not DateTime timestamp)
            {
                Structure.RemoveChildren(GedcomTags.DATE);
                return;
            }

            Date = GedcomDate.FromDateOnly(DateOnly.FromDateTime(timestamp));
            string time = timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            Time = Version.IsSeven && timestamp.Kind == DateTimeKind.Utc ? time + "Z" : time;
        }
    }
}

/// <summary>A <c>NOTE</c> or <c>SNOTE</c> under another structure: inline text, or a pointer to a shared note record.</summary>
public sealed class NoteLink(GedcomStructure structure) : GedcomCitableView(structure)
{
    public bool IsReference => Structure.HasPointer;

    public string? NoteXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    /// <summary>The inline text, or the shared note's text when this is a reference in a file.</summary>
    public string? Text
    {
        get => IsReference ? Resolve()?.Text : Value;
        set => Value = value;
    }

    public NoteRecord? Resolve() => IsReference && File?.Find(Structure.Pointer!) is { } record && record.Tag is GedcomTags.NOTE or GedcomTags.SNOTE
        ? new NoteRecord(record)
        : null;

    /// <summary>The 7.0 <c>MIME</c> of the note text (<c>text/plain</c> or <c>text/html</c>).</summary>
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
}

/// <summary>A source citation (<c>SOUR</c> under another structure): a pointer to a source record, or 5.x embedded source text.</summary>
public sealed class SourceCitation(GedcomStructure structure) : GedcomCitableView(structure)
{
    public bool IsReference => Structure.HasPointer;

    public string? SourceXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    /// <summary>The description of an embedded (5.x, pointer-less) source.</summary>
    public string? EmbeddedText => IsReference ? null : Value;

    public SourceRecord? Resolve() => IsReference && File?.Find(Structure.Pointer!) is { Tag: GedcomTags.SOUR } record
        ? new SourceRecord(record)
        : null;

    /// <summary>Where in the source the information is (<c>PAGE</c>), e.g. "Film 1234, p. 5".</summary>
    public string? Page
    {
        get => Get(GedcomTags.PAGE);
        set => Set(GedcomTags.PAGE, value);
    }

    /// <summary>Certainty assessment (<c>QUAY</c>) from 0 (unreliable) to 3 (direct evidence).</summary>
    public int? Quality
    {
        get => int.TryParse(Get(GedcomTags.QUAY), NumberStyles.None, CultureInfo.InvariantCulture, out int quality) ? quality : null;
        set => Set(GedcomTags.QUAY, value?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The event type the source recorded (<c>EVEN</c>), e.g. BIRT.</summary>
    public string? EventType
    {
        get => Get(GedcomTags.EVEN);
        set => Set(GedcomTags.EVEN, value);
    }

    public string? Role
    {
        get => Structure.Child(GedcomTags.EVEN)?.ChildValue(GedcomTags.ROLE);
        set => Structure.GetOrAddChild(GedcomTags.EVEN).SetChildValue(GedcomTags.ROLE, value);
    }

    /// <summary>When the cited entry was recorded (<c>DATA.DATE</c>).</summary>
    public GedcomDate EntryDate
    {
        get => Structure.Child(GedcomTags.DATA) is { } data ? new SourceCitationData(data).Date : GedcomDate.Empty;
        set => new SourceCitationData(Structure.GetOrAddChild(GedcomTags.DATA)).Date = value;
    }

    /// <summary>Transcribed text from the source (<c>DATA.TEXT</c>; CONT lines are joined with newlines).</summary>
    public IEnumerable<string> Texts => Structure.Child(GedcomTags.DATA)?.ChildrenWithTag(GedcomTags.TEXT).Select(text => text.Value ?? string.Empty) ?? [];

    public void AddText(string text) => Structure.GetOrAddChild(GedcomTags.DATA).AddChild(GedcomTags.TEXT, text);

    private sealed class SourceCitationData(GedcomStructure structure) : GedcomStructureView(structure)
    {
        public GedcomDate Date
        {
            get => GetDate();
            set => SetDate(value);
        }
    }
}

/// <summary>An <c>OBJE</c> under another structure: a pointer to a multimedia record, or a 5.x embedded object.</summary>
public sealed class MultimediaLink(GedcomStructure structure) : GedcomCitableView(structure)
{
    public bool IsReference => Structure.HasPointer;

    public string? MultimediaXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    public MultimediaRecord? Resolve() => IsReference && File?.Find(Structure.Pointer!) is { Tag: GedcomTags.OBJE } record
        ? new MultimediaRecord(record)
        : null;

    /// <summary>The link's own title (7.0), or the title of an embedded 5.x object.</summary>
    public string? Title
    {
        get => GetFirst(GedcomTags.TITL, "_TITL");
        set => Set(GedcomTags.TITL, value);
    }

    /// <summary>Files of an embedded 5.x object (empty for references).</summary>
    public IEnumerable<MultimediaFile> Files => IsReference ? [] : Views(GedcomTags.FILE, child => new MultimediaFile(child));

    /// <summary>
    /// The primary/preferred-photo flag that most desktop programs write as <c>_PRIM Y</c> (or <c>_PRIMARY Y</c>).
    /// It is not standard GEDCOM but is the only common way to mark a profile picture.
    /// </summary>
    public bool IsPrimary
    {
        get => GetFirst(GedcomTags.Extensions.PRIM, GedcomTags.Extensions.PRIMARY) is { } flag && flag.Trim().StartsWith('Y');
        set
        {
            Structure.RemoveChildren(GedcomTags.Extensions.PRIMARY);
            Set(GedcomTags.Extensions.PRIM, value ? "Y" : null);
        }
    }

    /// <summary>7.0 <c>CROP</c> region (percentages of the image).</summary>
    public GedcomStructure? Crop => Structure.Child(GedcomTags.CROP);
}

/// <summary><c>ASSO</c>: a link to an associated individual and their role, e.g. a godparent or witness.</summary>
public sealed class Association(GedcomStructure structure) : GedcomCitableView(structure)
{
    public string? IndividualXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    /// <summary>The role: 7.0 <c>ROLE</c> (with its PHRASE when it is OTHER) or 5.x <c>RELA</c>.</summary>
    public string? Relation
    {
        get
        {
            if (Structure.Child(GedcomTags.ROLE) is { } role)
            {
                return role.Value == "OTHER" && role.ChildValue(GedcomTags.PHRASE) is { } phrase ? phrase : role.Value;
            }

            return Get(GedcomTags.RELA);
        }

        set
        {
            if (Version.IsSeven)
            {
                Structure.RemoveChildren(GedcomTags.RELA);
                var role = Structure.SetChildValue(GedcomTags.ROLE, value is null ? null : GedcomEnumerations.ToRole(value, out _));
                if (role is not null && value is not null)
                {
                    role.SetChildValue(GedcomTags.PHRASE, role.Value == "OTHER" ? value : null);
                }
            }
            else
            {
                Set(GedcomTags.RELA, value);
            }
        }
    }

    public IndividualRecord? Resolve() => File?.Find<IndividualRecord>(IndividualXref);
}

/// <summary>A <c>REPO</c> citation inside a source record, with call numbers.</summary>
public sealed class RepositoryCitation(GedcomStructure structure) : GedcomCitableView(structure)
{
    public string? RepositoryXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    public IEnumerable<string> CallNumbers => Values(GedcomTags.CALN);

    public RepositoryRecord? Resolve() => File?.Find<RepositoryRecord>(RepositoryXref);
}
