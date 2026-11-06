namespace GedcomSharp.Model;

/// <summary>
/// A level-0 record. Record types this library knows get a subclass (<see cref="IndividualRecord"/>, ...);
/// extension records such as <c>_LOC</c> are plain <see cref="GedcomRecord"/>s with the same common members.
/// </summary>
public class GedcomRecord : GedcomCitableView
{
    /// <summary>
    /// Initializes a new instance of the GedcomRecord class.
    /// </summary>
    /// <param name="structure">The structure.</param>
    public GedcomRecord(GedcomStructure structure)
        : base(structure)
    {
    }

    /// <summary>Wraps a record structure in its typed view.</summary>
    public static GedcomRecord Create(GedcomStructure structure) => structure.Tag switch
    {
        GedcomTags.INDI => new IndividualRecord(structure),
        GedcomTags.FAM => new FamilyRecord(structure),
        GedcomTags.SOUR => new SourceRecord(structure),
        GedcomTags.REPO => new RepositoryRecord(structure),
        GedcomTags.NOTE or GedcomTags.SNOTE => new NoteRecord(structure),
        GedcomTags.OBJE => new MultimediaRecord(structure),
        GedcomTags.SUBM => new SubmitterRecord(structure),
        GedcomTags.SUBN => new SubmissionRecord(structure),
        _ => new GedcomRecord(structure)
    };

    /// <summary>
    /// Gets or sets the xref.
    /// </summary>
    public string? Xref
    {
        get => Structure.Xref;
        set => Structure.Xref = value;
    }

    /// <summary>
    /// Gets the user references.
    /// </summary>
    public IEnumerable<UserReference> UserReferences => Views(GedcomTags.REFN, child => new UserReference(child));

    /// <summary>
    /// Adds the user reference.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="type">The type.</param>
    /// <returns>The user reference.</returns>
    public UserReference AddUserReference(string number, string? type = null)
    {
        var reference = new UserReference(Structure.AddChild(GedcomTags.REFN, number));
        reference.Type = type;
        return reference;
    }

    /// <summary>The 5.x <c>RIN</c>, which 7.0 files carry as <c>_RIN</c> after conversion.</summary>
    public string? AutomatedRecordId
    {
        get => GetFirst(GedcomTags.RIN, GedcomTags.Extensions.RIN);
        set => Set(Version.IsSeven ? GedcomTags.Extensions.RIN : GedcomTags.RIN, value);
    }

    /// <summary>Unique identifiers: 7.0 <c>UID</c> and the widely used 5.x extension <c>_UID</c>.</summary>
    public IEnumerable<string> Uids => Values(GedcomTags.UID, GedcomTags.Extensions.UID);

    /// <summary>
    /// Gets or sets the uid.
    /// </summary>
    public string? Uid
    {
        get => Uids.FirstOrDefault();
        set
        {
            Structure.RemoveChildren(GedcomTags.UID);
            Structure.RemoveChildren(GedcomTags.Extensions.UID);
            if (!string.IsNullOrEmpty(value))
            {
                Structure.AddChild(Version.IsSeven ? GedcomTags.UID : GedcomTags.Extensions.UID, value);
            }
        }
    }

    /// <summary>
    /// Gets the external ids.
    /// </summary>
    public IEnumerable<ExternalId> ExternalIds => Views(GedcomTags.EXID, child => new ExternalId(child));

    /// <summary>
    /// Adds the external identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="type">The type.</param>
    /// <returns>The external identifier.</returns>
    public ExternalId AddExternalId(string id, string? type = null)
    {
        var externalId = new ExternalId(Structure.AddChild(GedcomTags.EXID, id));
        externalId.Type = type;
        return externalId;
    }

    /// <summary>
    /// Gets the changed.
    /// </summary>
    public ChangeDate? Changed => Structure.Child(GedcomTags.CHAN) is { } change ? new ChangeDate(change) : null;

    /// <summary>The 7.0 creation date (<c>CREA</c>).</summary>
    public ChangeDate? Created => Structure.Child(GedcomTags.CREA) is { } created ? new ChangeDate(created) : null;

    /// <summary>Sets <c>CHAN.DATE</c> and <c>CHAN.TIME</c>, as an application should when it edits a record.</summary>
    public ChangeDate SetChanged(DateTime timestamp)
    {
        var change = new ChangeDate(Structure.GetOrAddChild(GedcomTags.CHAN));
        change.Timestamp = timestamp;
        return change;
    }

    /// <summary>The restriction notice (<c>RESN</c>): <c>CONFIDENTIAL</c>, <c>LOCKED</c>, <c>PRIVACY</c> (comma-separated in 7.0).</summary>
    public string? Restriction
    {
        get => Get(GedcomTags.RESN);
        set => Set(GedcomTags.RESN, value);
    }

    /// <summary>
    /// Gets a value indicating whether this instance is private.
    /// </summary>
    public bool IsPrivate => Restriction?.Contains("PRIVACY", StringComparison.OrdinalIgnoreCase) == true
        || Restriction?.Contains("CONFIDENTIAL", StringComparison.OrdinalIgnoreCase) == true;
}
