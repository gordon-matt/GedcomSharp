namespace GedcomSharp.Model;

/// <summary>
/// <c>FAM</c>: a couple and/or their children. <c>HUSB</c> and <c>WIFE</c> are the GEDCOM names for the two
/// partners; they do not imply sex or marriage.
/// </summary>
public sealed class FamilyRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    public string? HusbandXref
    {
        get => Structure.Child(GedcomTags.HUSB)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.HUSB, value);
    }

    public string? WifeXref
    {
        get => Structure.Child(GedcomTags.WIFE)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.WIFE, value);
    }

    public IEnumerable<string> ChildXrefs => Structure.ChildrenWithTag(GedcomTags.CHIL)
        .Where(child => child.HasPointer && !child.IsVoidPointer)
        .Select(child => child.Pointer!);

    public IndividualRecord? Husband => File?.Find<IndividualRecord>(HusbandXref);

    public IndividualRecord? Wife => File?.Find<IndividualRecord>(WifeXref);

    public IEnumerable<IndividualRecord> Partners => new[] { Husband, Wife }.OfType<IndividualRecord>();

    public IEnumerable<IndividualRecord> Children => ChildXrefs.Select(xref => File?.Find<IndividualRecord>(xref)).OfType<IndividualRecord>();

    public GedcomStructure AddChild(string individualXref) => Structure.AddPointer(GedcomTags.CHIL, individualXref);

    public bool RemoveChild(string individualXref)
    {
        var link = Structure.ChildrenWithTag(GedcomTags.CHIL).FirstOrDefault(child => child.Pointer == individualXref);
        return link is not null && Structure.Children.Remove(link);
    }

    public IEnumerable<GedcomEvent> Events => Structure.Children
        .Where(child => GedcomEvent.IsEventTag(child, family: true))
        .Select(child => new GedcomEvent(child));

    public IEnumerable<GedcomEvent> GetEvents(string tag) => Views(tag, child => new GedcomEvent(child));

    public GedcomEvent? GetEvent(string tag) => Structure.Child(tag) is { } found ? new GedcomEvent(found) : null;

    public GedcomEvent? Marriage => GetEvent(GedcomTags.MARR);

    public GedcomEvent? Divorce => GetEvent(GedcomTags.DIV);

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

    public int? NumberOfChildren
    {
        get => int.TryParse(Get(GedcomTags.NCHI), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : null;
        set => Set(GedcomTags.NCHI, value?.ToString(CultureInfo.InvariantCulture));
    }

    public IEnumerable<LdsOrdinance> LdsSealings => Views(GedcomTags.SLGS, child => new LdsOrdinance(child));

    public IEnumerable<Association> Associations => Views(GedcomTags.ASSO, child => new Association(child));

    public IEnumerable<string> SubmitterXrefs => Structure.ChildrenWithTag(GedcomTags.SUBM).Where(submitter => submitter.HasPointer).Select(submitter => submitter.Pointer!);
}
