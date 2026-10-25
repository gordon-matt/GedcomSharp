namespace GedcomSharp.Model;

/// <summary>
/// An event or attribute of an individual or family: <c>BIRT</c>, <c>MARR</c>, <c>OCCU</c>, <c>EVEN</c>, <c>FACT</c>
/// or an extension tag with a date or place. Attributes carry their value in <see cref="GedcomStructureView.Value"/>;
/// events may have <c>Y</c> there to say "it happened" without details.
/// </summary>
public sealed class GedcomEvent(GedcomStructure structure) : GedcomCitableView(structure)
{
    public bool IsAttribute => GedcomTags.IndividualAttributes.Contains(Tag) && Tag != GedcomTags.RESI;

    /// <summary>The classification of a generic <c>EVEN</c>/<c>FACT</c>, or a descriptor for others (<c>TYPE</c>).</summary>
    public string? Type
    {
        get => Get(GedcomTags.TYPE);
        set => Set(GedcomTags.TYPE, value);
    }

    public GedcomDate Date
    {
        get => GetDate();
        set => SetDate(value);
    }

    /// <summary>The DATE value as written, for display of dates this library could not parse.</summary>
    public string? DateText => Get(GedcomTags.DATE);

    public Place? Place => Structure.Child(GedcomTags.PLAC) is { } place ? new Place(place) : null;

    public string? PlaceName
    {
        get => Get(GedcomTags.PLAC);
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                Structure.RemoveChildren(GedcomTags.PLAC);
                return;
            }

            Structure.GetOrAddChild(GedcomTags.PLAC).Value = value;
        }
    }

    public ContactDetails Contact => new(Structure);

    public GedcomAge? Age
    {
        get => GedcomAge.Parse(Get(GedcomTags.AGE));
        set => Set(GedcomTags.AGE, value?.Format(Version));
    }

    public string? Cause
    {
        get => Get(GedcomTags.CAUS);
        set => Set(GedcomTags.CAUS, value);
    }

    /// <summary>The responsible organisation (<c>AGNC</c>).</summary>
    public string? Agency
    {
        get => Get(GedcomTags.AGNC);
        set => Set(GedcomTags.AGNC, value);
    }

    public string? Religion
    {
        get => Get(GedcomTags.RELI);
        set => Set(GedcomTags.RELI, value);
    }

    public string? Restriction
    {
        get => Get(GedcomTags.RESN);
        set => Set(GedcomTags.RESN, value);
    }

    /// <summary>The husband's age at a family event (<c>HUSB.AGE</c>).</summary>
    public GedcomAge? HusbandAge => GedcomAge.Parse(Structure.Child(GedcomTags.HUSB)?.ChildValue(GedcomTags.AGE));

    /// <summary>The wife's age at a family event (<c>WIFE.AGE</c>).</summary>
    public GedcomAge? WifeAge => GedcomAge.Parse(Structure.Child(GedcomTags.WIFE)?.ChildValue(GedcomTags.AGE));

    /// <summary>People who took part in the event (7.0 <c>ASSO</c> under the event; 5.x writers used <c>_ASSO</c>).</summary>
    public IEnumerable<Association> Associations => Views([GedcomTags.ASSO, "_ASSO"], child => new Association(child));

    /// <summary>For adoptions, the family adopted into (<c>FAMC</c>).</summary>
    public string? FamilyXref
    {
        get => Structure.Child(GedcomTags.FAMC)?.Pointer;
        set => Structure.SetChildPointer(GedcomTags.FAMC, value);
    }

    /// <summary>
    /// Standard event and attribute tags, plus extension tags that have a DATE or PLAC. 7.0 <c>NO</c> structures
    /// ("never married") say an event did not happen, so they are not events.
    /// </summary>
    internal static bool IsEventTag(GedcomStructure structure, bool family)
    {
        var standard = family ? GedcomTags.FamilyEvents : GedcomTags.IndividualEvents;
        if (standard.Contains(structure.Tag) || (!family && GedcomTags.IndividualAttributes.Contains(structure.Tag)))
        {
            return true;
        }

        return structure.IsExtension && (structure.Child(GedcomTags.DATE) is not null || structure.Child(GedcomTags.PLAC) is not null);
    }
}

/// <summary><c>PLAC</c>: a place name (jurisdictions separated by commas), with optional coordinates and translations.</summary>
public sealed class Place(GedcomStructure structure) : GedcomCitableView(structure)
{
    public string? Name
    {
        get => Value;
        set => Value = value;
    }

    /// <summary>The jurisdictions, smallest first: "Town, County, Country" gives [Town, County, Country].</summary>
    public IReadOnlyList<string> Jurisdictions => (Value ?? string.Empty).Split(',').Select(part => part.Trim()).ToList();

    /// <summary>The jurisdiction names of each part (<c>FORM</c>), e.g. "City, County, State, Country".</summary>
    public string? Form
    {
        get => Get(GedcomTags.FORM);
        set => Set(GedcomTags.FORM, value);
    }

    public double? Latitude
    {
        get => GedcomCoordinates.Parse(Structure.Child(GedcomTags.MAP, "_MAP")?.ChildValue(GedcomTags.LATI), isLatitude: true);
        set => SetCoordinate(GedcomTags.LATI, value, isLatitude: true);
    }

    public double? Longitude
    {
        get => GedcomCoordinates.Parse(Structure.Child(GedcomTags.MAP, "_MAP")?.ChildValue(GedcomTags.LONG), isLatitude: false);
        set => SetCoordinate(GedcomTags.LONG, value, isLatitude: false);
    }

    public IEnumerable<Place> Translations => Views([GedcomTags.TRAN, GedcomTags.FONE, GedcomTags.ROMN], child => new Place(child));

    private void SetCoordinate(string tag, double? value, bool isLatitude)
    {
        var map = Structure.Child(GedcomTags.MAP, "_MAP");
        if (value is not double degrees)
        {
            map?.RemoveChildren(tag);
            if (map is { HasChildren: false })
            {
                map.Remove();
            }

            return;
        }

        map ??= Structure.AddChild(Version == GedcomVersion.V55 ? "_MAP" : GedcomTags.MAP);
        map.SetChildValue(tag, GedcomCoordinates.Format(degrees, isLatitude));
    }
}

/// <summary>
/// An address plus the phone, email, fax and web structures that sit beside it. Reads accept the 5.x vendor
/// variants (<c>EMAI</c>, <c>_EMAIL</c>, <c>_FAX</c>, <c>_WWW</c>, <c>_URL</c>) so that contact details from any exporter appear.
/// </summary>
public sealed class ContactDetails(GedcomStructure structure) : GedcomStructureView(structure)
{
    public Address? Address => Structure.Child(GedcomTags.ADDR) is { } address ? new Address(address) : null;

    public Address GetOrAddAddress() => new(Structure.GetOrAddChild(GedcomTags.ADDR));

    public IEnumerable<string> Phones => Values(GedcomTags.PHON);

    public IEnumerable<string> Emails => Values(GedcomTags.EMAIL, GedcomTags.EMAI, GedcomTags.Extensions.EMAIL);

    public IEnumerable<string> Faxes => Values(GedcomTags.FAX, GedcomTags.Extensions.FAX);

    public IEnumerable<string> Websites => Values(GedcomTags.WWW, GedcomTags.Extensions.WWW, GedcomTags.Extensions.URL);

    public void AddPhone(string phone) => Structure.AddChild(GedcomTags.PHON, phone);

    public void AddEmail(string email) => Structure.AddChild(Version == GedcomVersion.V55 ? GedcomTags.Extensions.EMAIL : GedcomTags.EMAIL, email);

    public void AddFax(string fax) => Structure.AddChild(Version == GedcomVersion.V55 ? GedcomTags.Extensions.FAX : GedcomTags.FAX, fax);

    public void AddWebsite(string url) => Structure.AddChild(Version == GedcomVersion.V55 ? GedcomTags.Extensions.WWW : GedcomTags.WWW, url);
}

/// <summary><c>ADDR</c>: the address as free text (lines joined with newlines) plus optional parsed parts.</summary>
public sealed class Address(GedcomStructure structure) : GedcomStructureView(structure)
{
    public string? Lines
    {
        get => Value;
        set => Value = value;
    }

    public string? Line1
    {
        get => Get(GedcomTags.ADR1);
        set => Set(GedcomTags.ADR1, value);
    }

    public string? Line2
    {
        get => Get(GedcomTags.ADR2);
        set => Set(GedcomTags.ADR2, value);
    }

    public string? Line3
    {
        get => Get(GedcomTags.ADR3);
        set => Set(GedcomTags.ADR3, value);
    }

    public string? City
    {
        get => Get(GedcomTags.CITY);
        set => Set(GedcomTags.CITY, value);
    }

    public string? State
    {
        get => Get(GedcomTags.STAE);
        set => Set(GedcomTags.STAE, value);
    }

    public string? PostalCode
    {
        get => Get(GedcomTags.POST);
        set => Set(GedcomTags.POST, value);
    }

    public string? Country
    {
        get => Get(GedcomTags.CTRY);
        set => Set(GedcomTags.CTRY, value);
    }

    /// <summary>The address as one string: the free text, or the parts when there is no free text.</summary>
    public string FullText => Value is { Length: > 0 } text
        ? text
        : string.Join(", ", new[] { Line1, Line2, Line3, City, State, PostalCode, Country }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
