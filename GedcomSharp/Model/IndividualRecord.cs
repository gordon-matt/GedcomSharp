namespace GedcomSharp.Model;

/// <summary><c>INDI</c>: a person.</summary>
public sealed class IndividualRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    public IEnumerable<PersonalName> Names => Views(GedcomTags.NAME, child => new PersonalName(child));

    public PersonalName? PrimaryName => Structure.Child(GedcomTags.NAME) is { } name ? new PersonalName(name) : null;

    public string DisplayName => PrimaryName?.DisplayName ?? string.Empty;

    public PersonalName AddName(string value) => new(Structure.AddChild(GedcomTags.NAME, value));

    /// <summary>Adds a NAME with the value built from the parts, plus GIVN and SURN.</summary>
    public PersonalName AddName(string? given, string? surname)
    {
        var name = AddName(PersonalName.FormatValue(given, surname));
        name.Given = given;
        name.Surname = surname;
        return name;
    }

    public GedcomSex Sex
    {
        get => GedcomSexExtensions.ParseSex(Get(GedcomTags.SEX));
        set => Set(GedcomTags.SEX, value.ToCode());
    }

    /// <summary>Events and attributes in file order, including extension events with a date or place.</summary>
    public IEnumerable<GedcomEvent> Events => Structure.Children
        .Where(child => GedcomEvent.IsEventTag(child, family: false))
        .Select(child => new GedcomEvent(child));

    public IEnumerable<GedcomEvent> GetEvents(string tag) => Views(tag, child => new GedcomEvent(child));

    public GedcomEvent? GetEvent(string tag) => Structure.Child(tag) is { } found ? new GedcomEvent(found) : null;

    public GedcomEvent? Birth => GetEvent(GedcomTags.BIRT);

    public GedcomEvent? Christening => GetEvent(GedcomTags.CHR) ?? GetEvent(GedcomTags.BAPM);

    public GedcomEvent? Death => GetEvent(GedcomTags.DEAT);

    public GedcomEvent? Burial => GetEvent(GedcomTags.BURI) ?? GetEvent(GedcomTags.CREM);

    /// <summary>Adds an event or attribute. Pass <paramref name="value"/> for attributes (OCCU, RELI, ...).</summary>
    public GedcomEvent AddEvent(string tag, GedcomDate? date = null, string? place = null, string? value = null)
    {
        var added = new GedcomEvent(Structure.AddChild(tag, value));
        if (date is not null)
        {
            added.Date = date;
        }

        if (place is not null)
        {
            added.PlaceName = place;
        }

        return added;
    }

    public IEnumerable<ChildToFamilyLink> ChildToFamilyLinks => Views(GedcomTags.FAMC, child => new ChildToFamilyLink(child));

    public IEnumerable<SpouseToFamilyLink> SpouseToFamilyLinks => Views(GedcomTags.FAMS, child => new SpouseToFamilyLink(child));

    public IEnumerable<Association> Associations => Views(GedcomTags.ASSO, child => new Association(child));

    public IEnumerable<LdsOrdinance> LdsOrdinances => Structure.Children
        .Where(child => GedcomTags.LdsOrdinances.Contains(child.Tag))
        .Select(child => new LdsOrdinance(child));

    /// <summary>Pointers to records of other individuals who may be the same person (<c>ALIA</c>).</summary>
    public IEnumerable<string> AliasXrefs => Structure.ChildrenWithTag(GedcomTags.ALIA).Where(alias => alias.HasPointer).Select(alias => alias.Pointer!);

    public IEnumerable<string> SubmitterXrefs => Structure.ChildrenWithTag(GedcomTags.SUBM).Where(submitter => submitter.HasPointer).Select(submitter => submitter.Pointer!);

    /// <summary>The families this person is a child in, resolved in the file.</summary>
    public IEnumerable<FamilyRecord> ParentFamilies => ChildToFamilyLinks.Select(link => link.Resolve()).OfType<FamilyRecord>();

    /// <summary>The families this person is a partner in, resolved in the file.</summary>
    public IEnumerable<FamilyRecord> SpouseFamilies => SpouseToFamilyLinks.Select(link => link.Resolve()).OfType<FamilyRecord>();

    public IEnumerable<IndividualRecord> Parents => ParentFamilies.SelectMany(family => family.Partners).Distinct();

    public IEnumerable<IndividualRecord> Spouses => SpouseFamilies
        .SelectMany(family => family.Partners)
        .Where(partner => !ReferenceEquals(partner.Structure, Structure))
        .Distinct();

    public IEnumerable<IndividualRecord> Children => SpouseFamilies.SelectMany(family => family.Children).Distinct();

    public ChildToFamilyLink AddChildToFamilyLink(string familyXref, string? pedigree = null)
    {
        var link = new ChildToFamilyLink(Structure.AddPointer(GedcomTags.FAMC, familyXref));
        if (pedigree is not null)
        {
            link.Pedigree = pedigree;
        }

        return link;
    }

    public SpouseToFamilyLink AddSpouseToFamilyLink(string familyXref) => new(Structure.AddPointer(GedcomTags.FAMS, familyXref));

    /// <summary>The preferred photo: the first OBJE link marked <c>_PRIM Y</c>, otherwise the first link.</summary>
    public MultimediaLink? PrimaryMediaLink => MediaLinks.FirstOrDefault(link => link.IsPrimary) ?? MediaLinks.FirstOrDefault();
}
