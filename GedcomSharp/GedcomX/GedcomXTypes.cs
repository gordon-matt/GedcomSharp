namespace GedcomSharp.GedcomX;

/// <summary>The GEDCOM X media types and the URIs of the types the specifications define.</summary>
public static class GedcomXTypes
{
    public const string JSON_MEDIA_TYPE = "application/x-gedcomx-v1+json";
    public const string XML_MEDIA_TYPE = "application/x-gedcomx-v1+xml";
    public const string XML_NAMESPACE = "http://gedcomx.org/v1/";
    public const string FILE_CONFORMS_TO = "http://gedcomx.org/file/v1";

    private const string BASE = "http://gedcomx.org/";

    public static class Gender
    {
        public const string MALE = BASE + "Male";
        public const string FEMALE = BASE + "Female";
        public const string UNKNOWN = BASE + "Unknown";
        public const string INTERSEX = BASE + "Intersex";
    }

    public static class Relationship
    {
        public const string COUPLE = BASE + "Couple";
        public const string PARENT_CHILD = BASE + "ParentChild";
        public const string ENSLAVED_BY = BASE + "EnslavedBy";
        public const string GODPARENT = BASE + "Godparent";
    }

    public static class NameType
    {
        public const string BIRTH_NAME = BASE + "BirthName";
        public const string MARRIED_NAME = BASE + "MarriedName";
        public const string ALSO_KNOWN_AS = BASE + "AlsoKnownAs";
        public const string NICKNAME = BASE + "Nickname";
        public const string ADOPTIVE_NAME = BASE + "AdoptiveName";
        public const string FORMAL_NAME = BASE + "FormalName";
        public const string RELIGIOUS_NAME = BASE + "ReligiousName";
    }

    public static class NamePart
    {
        public const string PREFIX = BASE + "Prefix";
        public const string SUFFIX = BASE + "Suffix";
        public const string GIVEN = BASE + "Given";
        public const string SURNAME = BASE + "Surname";
    }

    public static class Confidence
    {
        public const string HIGH = BASE + "High";
        public const string MEDIUM = BASE + "Medium";
        public const string LOW = BASE + "Low";
    }

    public static class Identifier
    {
        public const string PRIMARY = BASE + "Primary";
        public const string AUTHORITY = BASE + "Authority";
        public const string DEPRECATED = BASE + "Deprecated";
    }

    public static class ResourceType
    {
        public const string COLLECTION = BASE + "Collection";
        public const string PHYSICAL_ARTIFACT = BASE + "PhysicalArtifact";
        public const string DIGITAL_ARTIFACT = BASE + "DigitalArtifact";
        public const string RECORD = BASE + "Record";
    }

    public static class EventRole
    {
        public const string PRINCIPAL = BASE + "Principal";
        public const string PARTICIPANT = BASE + "Participant";
        public const string OFFICIAL = BASE + "Official";
        public const string WITNESS = BASE + "Witness";
    }

    /// <summary>
    /// Fact types (<c>http://gedcomx.org/Birth</c>, ...) for the GEDCOM events and attributes that have one.
    /// Used by <see cref="GedcomXConverter"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FactTypesByGedcomTag { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [GedcomTags.ADOP] = BASE + "Adoption",
        [GedcomTags.BAPM] = BASE + "Baptism",
        [GedcomTags.BARM] = BASE + "BarMitzvah",
        [GedcomTags.BASM] = BASE + "BatMitzvah",
        [GedcomTags.BIRT] = BASE + "Birth",
        [GedcomTags.BLES] = BASE + "Blessing",
        [GedcomTags.BURI] = BASE + "Burial",
        [GedcomTags.CAST] = BASE + "Caste",
        [GedcomTags.CENS] = BASE + "Census",
        [GedcomTags.CHR] = BASE + "Christening",
        [GedcomTags.CHRA] = BASE + "AdultChristening",
        [GedcomTags.CONF] = BASE + "Confirmation",
        [GedcomTags.CREM] = BASE + "Cremation",
        [GedcomTags.DEAT] = BASE + "Death",
        [GedcomTags.DSCR] = BASE + "PhysicalDescription",
        [GedcomTags.EDUC] = BASE + "Education",
        [GedcomTags.EMIG] = BASE + "Emigration",
        [GedcomTags.FCOM] = BASE + "FirstCommunion",
        [GedcomTags.GRAD] = BASE + "Graduation",
        [GedcomTags.IDNO] = BASE + "NationalId",
        [GedcomTags.IMMI] = BASE + "Immigration",
        [GedcomTags.NATI] = BASE + "Nationality",
        [GedcomTags.NATU] = BASE + "Naturalization",
        [GedcomTags.NCHI] = BASE + "NumberOfChildren",
        [GedcomTags.NMR] = BASE + "NumberOfMarriages",
        [GedcomTags.OCCU] = BASE + "Occupation",
        [GedcomTags.ORDN] = BASE + "Ordination",
        [GedcomTags.PROB] = BASE + "Probate",
        [GedcomTags.PROP] = BASE + "Property",
        [GedcomTags.RELI] = BASE + "Religion",
        [GedcomTags.RESI] = BASE + "Residence",
        [GedcomTags.RETI] = BASE + "Retirement",
        [GedcomTags.WILL] = BASE + "Will",
        [GedcomTags.ANUL] = BASE + "Annulment",
        [GedcomTags.DIV] = BASE + "Divorce",
        [GedcomTags.DIVF] = BASE + "DivorceFiling",
        [GedcomTags.ENGA] = BASE + "Engagement",
        [GedcomTags.MARB] = BASE + "MarriageBanns",
        [GedcomTags.MARC] = BASE + "MarriageContract",
        [GedcomTags.MARL] = BASE + "MarriageLicense",
        [GedcomTags.MARR] = BASE + "Marriage",
        [GedcomTags.MARS] = BASE + "MarriageSettlement",
        [GedcomTags.TITL] = BASE + "Title",
        [GedcomTags.SSN] = BASE + "NationalId"
    };
}
