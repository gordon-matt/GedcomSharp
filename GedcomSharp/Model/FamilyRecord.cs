namespace GedcomSharp.Model;

/// <summary>
/// <c>FAM</c>: a couple and/or their children. <c>HUSB</c> and <c>WIFE</c> are the GEDCOM names for the two
/// partners; they do not imply sex or marriage.
/// </summary>
public sealed class FamilyRecord(GedcomStructure structure) : GedcomRecord(structure)
{
    /// <summary>
    /// Gets or sets the husband xref.
    /// </summary>
    public string? HusbandXref
    {
        get => Structure.Child(GedcomTags.HUSB)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.HUSB, value);
    }

    /// <summary>
    /// Gets or sets the wife xref.
    /// </summary>
    public string? WifeXref
    {
        get => Structure.Child(GedcomTags.WIFE)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.WIFE, value);
    }

    /// <summary>
    /// Gets the child xrefs.
    /// </summary>
    public IEnumerable<string> ChildXrefs => Structure.ChildrenWithTag(GedcomTags.CHIL)
        .Where(child => child.HasPointer && !child.IsVoidPointer)
        .Select(child => child.Pointer!);

    /// <summary>
    /// Gets the husband.
    /// </summary>
    public IndividualRecord? Husband => File?.Find<IndividualRecord>(HusbandXref);

    /// <summary>
    /// Gets the wife.
    /// </summary>
    public IndividualRecord? Wife => File?.Find<IndividualRecord>(WifeXref);

    /// <summary>
    /// Gets the partners.
    /// </summary>
    public IEnumerable<IndividualRecord> Partners => new[] { Husband, Wife }.OfType<IndividualRecord>();

    /// <summary>
    /// Gets the children.
    /// </summary>
    public IEnumerable<IndividualRecord> Children => ChildXrefs.Select(xref => File?.Find<IndividualRecord>(xref)).OfType<IndividualRecord>();

    /// <summary>
    /// Adds the child.
    /// </summary>
    /// <param name="individualXref">The individual xref.</param>
    /// <returns>The child.</returns>
    public GedcomStructure AddChild(string individualXref) => Structure.AddPointer(GedcomTags.CHIL, individualXref);

    /// <summary>
    /// Removes the child.
    /// </summary>
    /// <param name="individualXref">The individual xref.</param>
    /// <returns>true when the condition is met; otherwise, false.</returns>
    public bool RemoveChild(string individualXref)
    {
        var link = Structure.ChildrenWithTag(GedcomTags.CHIL).FirstOrDefault(child => child.Pointer == individualXref);
        return link is not null && Structure.Children.Remove(link);
    }

    /// <summary>
    /// Gets the events.
    /// </summary>
    public IEnumerable<GedcomEvent> Events => Structure.Children
        .Where(child => GedcomEvent.IsEventTag(child, family: true))
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
    /// Gets the marriage.
    /// </summary>
    public GedcomEvent? Marriage => GetEvent(GedcomTags.MARR);

    /// <summary>
    /// Gets the divorce.
    /// </summary>
    public GedcomEvent? Divorce => GetEvent(GedcomTags.DIV);

    /// <summary>
    /// Adds the event.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="date">The date.</param>
    /// <param name="place">The place.</param>
    /// <param name="value">The value.</param>
    /// <returns>The event.</returns>
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
    /// Gets or sets the number of children.
    /// </summary>
    public int? NumberOfChildren
    {
        get => int.TryParse(Get(GedcomTags.NCHI), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : null;
        set => Set(GedcomTags.NCHI, value?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Gets the lds sealings.
    /// </summary>
    public IEnumerable<LdsOrdinance> LdsSealings => Views(GedcomTags.SLGS, child => new LdsOrdinance(child));

    /// <summary>
    /// Gets the associations.
    /// </summary>
    public IEnumerable<Association> Associations => Views(GedcomTags.ASSO, child => new Association(child));

    /// <summary>
    /// Gets the submitter xrefs.
    /// </summary>
    public IEnumerable<string> SubmitterXrefs => Structure.ChildrenWithTag(GedcomTags.SUBM).Where(submitter => submitter.HasPointer).Select(submitter => submitter.Pointer!);
}
