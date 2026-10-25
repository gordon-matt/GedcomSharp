namespace GedcomSharp.Model;

/// <summary><c>FAMC</c> under an individual: the family the individual is a child of, and how.</summary>
public sealed class ChildToFamilyLink(GedcomStructure structure) : GedcomCitableView(structure)
{
    public string? FamilyXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    /// <summary>
    /// <c>PEDI</c>: BIRTH, ADOPTED, FOSTER, SEALING or OTHER (7.0, uppercase) / birth, adopted, foster, sealing (5.5.1).
    /// Read case-insensitively; written in the file's style.
    /// </summary>
    public string? Pedigree
    {
        get => Get(GedcomTags.PEDI)?.ToUpperInvariant();
        set => Set(GedcomTags.PEDI, value is null ? null : Version.IsSeven ? value.ToUpperInvariant() : value.ToLowerInvariant());
    }

    /// <summary><c>STAT</c>: CHALLENGED, DISPROVEN or PROVEN.</summary>
    public string? Status
    {
        get => Get(GedcomTags.STAT)?.ToUpperInvariant();
        set => Set(GedcomTags.STAT, value is null ? null : Version.IsSeven ? value.ToUpperInvariant() : value.ToLowerInvariant());
    }

    public FamilyRecord? Resolve() => File?.Find<FamilyRecord>(FamilyXref);
}

/// <summary><c>FAMS</c> under an individual: a family in which the individual is a partner.</summary>
public sealed class SpouseToFamilyLink(GedcomStructure structure) : GedcomCitableView(structure)
{
    public string? FamilyXref
    {
        get => Structure.Pointer;
        set => Structure.Pointer = value;
    }

    public FamilyRecord? Resolve() => File?.Find<FamilyRecord>(FamilyXref);
}

/// <summary>An LDS ordinance: BAPL, CONL, ENDL, INIL, SLGC (individual) or SLGS (family).</summary>
public sealed class LdsOrdinance(GedcomStructure structure) : GedcomCitableView(structure)
{
    public GedcomDate Date
    {
        get => GetDate();
        set => SetDate(value);
    }

    public string? Temple
    {
        get => Get(GedcomTags.TEMP);
        set => Set(GedcomTags.TEMP, value);
    }

    public string? PlaceName
    {
        get => Get(GedcomTags.PLAC);
        set => Set(GedcomTags.PLAC, value);
    }

    public string? Status
    {
        get => Get(GedcomTags.STAT);
        set => Set(GedcomTags.STAT, value);
    }

    public GedcomDate StatusDate => Structure.Child(GedcomTags.STAT)?.Child(GedcomTags.DATE) is { } date
        ? GedcomDate.Parse(date.Value, date.ChildValue(GedcomTags.PHRASE))
        : GedcomDate.Empty;

    /// <summary>For SLGC, the family the child was sealed to.</summary>
    public string? FamilyXref
    {
        get => Structure.Child(GedcomTags.FAMC)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.FAMC, value);
    }
}

/// <summary><c>FILE</c> of a multimedia record: a path or URL plus its format and title.</summary>
/// <remarks>5.5 put FORM and TITL beside FILE rather than under it; the getters fall back to those.</remarks>
public sealed class MultimediaFile(GedcomStructure structure) : GedcomStructureView(structure)
{
    /// <summary>The file reference: a 7.0 URL or a 5.x path.</summary>
    public string? Path
    {
        get => Value;
        set => Value = value;
    }

    /// <summary>The format: a 7.0 media type (<c>image/jpeg</c>) or a 5.x extension (<c>jpg</c>).</summary>
    public string? Format
    {
        get => Get(GedcomTags.FORM) ?? Structure.Parent?.ChildValue(GedcomTags.FORM);
        set => Set(GedcomTags.FORM, value);
    }

    /// <summary>The source medium: <c>FORM.TYP</c> in 5.5.1 (a common misspelling) or <c>FORM.MEDI</c>.</summary>
    public string? Medium
    {
        get => Structure.Child(GedcomTags.FORM)?.Child(GedcomTags.MEDI, "TYPE", "TYP")?.Value;
        set => Structure.GetOrAddChild(GedcomTags.FORM).SetChildValue(GedcomTags.MEDI, value);
    }

    public string? Title
    {
        get => Get(GedcomTags.TITL) ?? Structure.Parent?.ChildValue(GedcomTags.TITL);
        set => Set(GedcomTags.TITL, value);
    }

    /// <summary>A media type for the file, from FORM or the file extension.</summary>
    public string? MediaType => MediaTypes.FromFormat(Format) ?? MediaTypes.FromPath(Path);
}
