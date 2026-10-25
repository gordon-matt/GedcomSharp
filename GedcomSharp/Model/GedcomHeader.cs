namespace GedcomSharp.Model;

/// <summary><c>HEAD</c>: metadata about the dataset — the program that wrote it, when, the GEDCOM version and character set.</summary>
public sealed class GedcomHeader(GedcomStructure structure) : GedcomCitableView(structure)
{
    /// <summary>The exporting program's registered name (<c>SOUR</c>), e.g. <c>RootsMagic</c>.</summary>
    public string? SourceSystem
    {
        get => Get(GedcomTags.SOUR);
        set => Set(GedcomTags.SOUR, value);
    }

    public string? SourceVersion
    {
        get => Structure.Find(GedcomTags.SOUR, GedcomTags.VERS)?.Value;
        set => Structure.GetOrAddChild(GedcomTags.SOUR).SetChildValue(GedcomTags.VERS, value);
    }

    /// <summary>The program's display name (<c>SOUR.NAME</c>).</summary>
    public string? SourceName
    {
        get => Structure.Find(GedcomTags.SOUR, GedcomTags.NAME)?.Value;
        set => Structure.GetOrAddChild(GedcomTags.SOUR).SetChildValue(GedcomTags.NAME, value);
    }

    public string? SourceCorporation => Structure.Find(GedcomTags.SOUR, GedcomTags.CORP)?.Value;

    public string? Destination
    {
        get => Get(GedcomTags.DEST);
        set => Set(GedcomTags.DEST, value);
    }

    public GedcomDate Date
    {
        get => GetDate();
        set => SetDate(value);
    }

    public string? Time => Structure.Child(GedcomTags.DATE)?.ChildValue(GedcomTags.TIME);

    public string? SubmitterXref
    {
        get => Structure.Child(GedcomTags.SUBM)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.SUBM, value);
    }

    public string? SubmissionXref
    {
        get => Structure.Child(GedcomTags.SUBN)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.SUBN, value);
    }

    public string? Copyright
    {
        get => Get(GedcomTags.COPR);
        set => Set(GedcomTags.COPR, value);
    }

    public string? Language
    {
        get => Get(GedcomTags.LANG);
        set => Set(GedcomTags.LANG, value);
    }

    /// <summary>The 5.x file name the dataset was written to (<c>FILE</c>).</summary>
    public string? FileName => Get(GedcomTags.FILE);

    /// <summary>The declared character set (<c>CHAR</c>, 5.x only).</summary>
    public string? CharacterSet => Get(GedcomTags.CHAR);

    /// <summary>The declared GEDCOM version (<c>GEDC.VERS</c>) as written.</summary>
    public string? GedcomVersionText => Structure.Find(GedcomTags.GEDC, GedcomTags.VERS)?.Value;

    /// <summary>The default place hierarchy (<c>PLAC.FORM</c>), e.g. "City, County, State, Country".</summary>
    public string? PlaceForm
    {
        get => Structure.Find(GedcomTags.PLAC, GedcomTags.FORM)?.Value;
        set => Structure.GetOrAddChild(GedcomTags.PLAC).SetChildValue(GedcomTags.FORM, value);
    }

    public string? Note => Get(GedcomTags.NOTE);

    /// <summary>The 7.0 extension-tag declarations (<c>SCHMA.TAG</c>): tag to URI, e.g. <c>_SKYPEID</c> to <c>http://xmlns.com/foaf/0.1/skypeID</c>.</summary>
    public IReadOnlyDictionary<string, string> ExtensionTags
    {
        get
        {
            var tags = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var declaration in Structure.Child(GedcomTags.SCHMA)?.ChildrenWithTag(GedcomTags.TAG) ?? [])
            {
                string[] parts = (declaration.Value ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    tags.TryAdd(parts[0], parts[1]);
                }
            }

            return tags;
        }
    }

    /// <summary>Declares a 7.0 extension tag in <c>SCHMA</c>, replacing any existing declaration of the same tag.</summary>
    public void DeclareExtensionTag(string tag, string uri)
    {
        var schema = Structure.GetOrAddChild(GedcomTags.SCHMA);
        var existing = schema.ChildrenWithTag(GedcomTags.TAG).FirstOrDefault(declaration => declaration.Value?.Split(' ')[0] == tag);
        if (existing is not null)
        {
            existing.Value = $"{tag} {uri}";
            return;
        }

        schema.AddChild(GedcomTags.TAG, $"{tag} {uri}");
    }
}
