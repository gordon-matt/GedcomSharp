namespace GedcomSharp.Model;

/// <summary><c>INDI</c>: a person.</summary>
public sealed class IndividualRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets the names.
    /// </summary>
    public IEnumerable<PersonalName> Names => Views(GedcomTags.NAME, child => new PersonalName(child));

    /// <summary>
    /// Gets the primary name.
    /// </summary>
    public PersonalName? PrimaryName => Structure.Child(GedcomTags.NAME) is { } name ? new PersonalName(name) : null;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => PrimaryName?.DisplayName ?? string.Empty;

    /// <summary>
    /// Adds the name.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    public PersonalName AddName(string value) => new(Structure.AddChild(GedcomTags.NAME, value));

    /// <summary>Adds a NAME with the value built from the parts, plus GIVN and SURN.</summary>
    public PersonalName AddName(string? given, string? surname)
    {
        var name = AddName(PersonalName.FormatValue(given, surname));
        name.Given = given;
        name.Surname = surname;
        return name;
    }

    /// <summary>
    /// Gets or sets the sex.
    /// </summary>
    public GedcomSex Sex
    {
        get => GedcomSexExtensions.ParseSex(Get(GedcomTags.SEX));
        set => Set(GedcomTags.SEX, value.ToCode());
    }

    /// <summary>Events and attributes in file order, including extension events with a date or place.</summary>
    public IEnumerable<GedcomEvent> Events => Structure.Children
        .Where(child => GedcomEvent.IsEventTag(child, family: false))
        .Select(child => new GedcomEvent(child));

    /// <summary>
    /// Gets the events.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The events.</returns>
    public IEnumerable<GedcomEvent> GetEvents(string tag) => Views(tag, child => new GedcomEvent(child));

    /// <summary>
    /// Gets the event.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The event.</returns>
    public GedcomEvent? GetEvent(string tag) => Structure.Child(tag) is { } found ? new GedcomEvent(found) : null;

    /// <summary>
    /// Gets the birth.
    /// </summary>
    public GedcomEvent? Birth => GetEvent(GedcomTags.BIRT);

    /// <summary>
    /// Gets the christening.
    /// </summary>
    public GedcomEvent? Christening => GetEvent(GedcomTags.CHR) ?? GetEvent(GedcomTags.BAPM);

    /// <summary>
    /// Gets the death.
    /// </summary>
    public GedcomEvent? Death => GetEvent(GedcomTags.DEAT);

    /// <summary>
    /// Gets the burial.
    /// </summary>
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

    /// <summary>
    /// Gets the child to family links.
    /// </summary>
    public IEnumerable<ChildToFamilyLink> ChildToFamilyLinks => Views(GedcomTags.FAMC, child => new ChildToFamilyLink(child));

    /// <summary>
    /// Gets the spouse to family links.
    /// </summary>
    public IEnumerable<SpouseToFamilyLink> SpouseToFamilyLinks => Views(GedcomTags.FAMS, child => new SpouseToFamilyLink(child));

    /// <summary>
    /// Gets the associations.
    /// </summary>
    public IEnumerable<Association> Associations => Views(GedcomTags.ASSO, child => new Association(child));

    /// <summary>
    /// Gets the lds ordinances.
    /// </summary>
    public IEnumerable<LdsOrdinance> LdsOrdinances => Structure.Children
        .Where(child => GedcomTags.LdsOrdinances.Contains(child.Tag))
        .Select(child => new LdsOrdinance(child));

    /// <summary>Pointers to records of other individuals who may be the same person (<c>ALIA</c>).</summary>
    public IEnumerable<string> AliasXrefs => Structure.ChildrenWithTag(GedcomTags.ALIA).Where(alias => alias.HasPointer).Select(alias => alias.Pointer!);

    /// <summary>
    /// Gets the submitter xrefs.
    /// </summary>
    public IEnumerable<string> SubmitterXrefs => Structure.ChildrenWithTag(GedcomTags.SUBM).Where(submitter => submitter.HasPointer).Select(submitter => submitter.Pointer!);

    /// <summary>The families this person is a child in, resolved in the file.</summary>
    public IEnumerable<FamilyRecord> ParentFamilies => ChildToFamilyLinks.Select(link => link.Resolve()).OfType<FamilyRecord>();

    /// <summary>The families this person is a partner in, resolved in the file.</summary>
    public IEnumerable<FamilyRecord> SpouseFamilies => SpouseToFamilyLinks.Select(link => link.Resolve()).OfType<FamilyRecord>();

    /// <summary>
    /// Gets the parents.
    /// </summary>
    public IEnumerable<IndividualRecord> Parents => ParentFamilies.SelectMany(family => family.Partners).Distinct();

    /// <summary>
    /// Gets the spouses.
    /// </summary>
    public IEnumerable<IndividualRecord> Spouses => SpouseFamilies
        .SelectMany(family => family.Partners)
        .Where(partner => !ReferenceEquals(partner.Structure, Structure))
        .Distinct();

    /// <summary>
    /// Gets the children.
    /// </summary>
    public IEnumerable<IndividualRecord> Children => SpouseFamilies.SelectMany(family => family.Children).Distinct();

    /// <summary>
    /// Adds the child to family link.
    /// </summary>
    /// <param name="familyXref">The family xref.</param>
    /// <param name="pedigree">The pedigree.</param>
    /// <returns>The child to family link.</returns>
    public ChildToFamilyLink AddChildToFamilyLink(string familyXref, string? pedigree = null)
    {
        var link = new ChildToFamilyLink(Structure.AddPointer(GedcomTags.FAMC, familyXref));
        if (pedigree is not null)
        {
            link.Pedigree = pedigree;
        }

        return link;
    }

    /// <summary>
    /// Adds the spouse to family link.
    /// </summary>
    /// <param name="familyXref">The family xref.</param>
    /// <returns>The spouse to family link.</returns>
    public SpouseToFamilyLink AddSpouseToFamilyLink(string familyXref) => new(Structure.AddPointer(GedcomTags.FAMS, familyXref));

    /// <summary>The preferred photo: the first OBJE link marked <c>_PRIM Y</c>, otherwise the first link.</summary>
    public MultimediaLink? PrimaryMediaLink => MediaLinks.FirstOrDefault(link => link.IsPrimary) ?? MediaLinks.FirstOrDefault();
}
